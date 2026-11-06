using System.Data;
using Microsoft.EntityFrameworkCore;
using Pawmart.Data;
using Pawmart.Middleware;
using Pawmart.Services.Integrations;

namespace Pawmart.Services;

public sealed class PaymentService(PawmartDbContext db, IPaymentGateway gateway)
{
    public async Task<PaymentStartResult> StartAsync(string userId, string orderId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var order = await db.Orders.SingleOrDefaultAsync(x => x.UserId == userId && x.OrderId == orderId, ct)
            ?? throw new ApiException(404, "Order was not found.", "Not found");
        if (order.OrderStatus != "PENDING") throw new ApiException(409, "Online payment can only be initiated for a pending order.", "Invalid order state");
        if (order.PaymentMethod == "COD") throw new ApiException(409, "Cash-on-delivery orders do not require online payment.", "Payment not required");
        if (order.PaymentMethod == "BANK_QR") throw new ApiException(409, "Use the bank QR instructions for this order.", "Payment method mismatch");
        var payment = await db.Payments.SingleOrDefaultAsync(x => x.OrderId == orderId, ct)
            ?? throw new ApiException(404, "Payment record was not found.", "Not found");
        if (payment.PaymentStatus == "SUCCESS") throw new ApiException(409, "This order has already been paid.", "Already paid");
        if (payment.PaymentStatus == "CANCELLED") throw new ApiException(409, "This payment has been cancelled.", "Payment not available");
        var existingReference = payment.TransactionCode;
        var start = await gateway.InitiateAsync(order, payment, payment.PaymentId, ct);
        if (existingReference is not null && !string.Equals(existingReference, start.PaymentReference, StringComparison.Ordinal))
            throw new ApiException(409, "The payment provider returned a different reference for the same payment attempt.", "Payment attempt conflict");
        payment.TransactionCode = start.PaymentReference;
        payment.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return start;
    }

    public async Task ApplyVerifiedCallbackAsync(IReadOnlyDictionary<string, string> values, CancellationToken ct)
    {
        var result = await gateway.VerifyCallbackAsync(values, ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var payment = await db.Payments.SingleOrDefaultAsync(x => x.OrderId == result.OrderId, ct)
            ?? throw new ApiException(404, "Payment record was not found.", "Not found");
        var order = await db.Orders.SingleAsync(x => x.OrderId == result.OrderId, ct);
        if (order.PaymentMethod == "COD") throw new ApiException(409, "Cash-on-delivery orders cannot receive an online payment callback.", "Payment method mismatch");
        if (order.PaymentMethod == "BANK_QR") throw new ApiException(409, "Bank QR requires bank reconciliation, not a gateway callback.", "Payment method mismatch");
        if (order.OrderStatus == "CANCELLED") throw new ApiException(409, "Cancelled orders cannot receive a successful payment callback.", "Order cancelled");
        if (payment.Amount != result.Amount) throw new ApiException(409, "Payment amount does not match the order.", "Payment amount mismatch");
        if (string.IsNullOrWhiteSpace(payment.TransactionCode) || !string.Equals(payment.TransactionCode, result.TransactionCode, StringComparison.Ordinal))
            throw new ApiException(409, "Payment callback does not match the initiated payment reference.", "Payment reference mismatch");
        if (payment.PaymentStatus == "SUCCESS")
        {
            await tx.CommitAsync(ct);
            return;
        }
        if (payment.PaymentStatus is "CANCELLED") throw new ApiException(409, "The payment has already been cancelled.", "Payment state conflict");

        payment.PaymentStatus = result.Succeeded ? "SUCCESS" : "FAILED";
        payment.ProviderTransactionId = result.ProviderTransactionId;
        payment.TransactionCode = result.TransactionCode;
        payment.PaidAt = result.Succeeded ? DateTime.UtcNow : null;
        payment.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
