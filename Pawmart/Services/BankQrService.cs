using System.Data;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Pawmart.Data;
using Pawmart.Middleware;
using Pawmart.Models.Contracts.Orders;
using Pawmart.Models.Entities;

namespace Pawmart.Services;

public sealed class BankQrService(PawmartDbContext db, IConfiguration configuration)
{
    private sealed record Receiver(string BankBin, string BankName, string AccountNo, string AccountName);

    public PaymentMethodsResponse GetMethods() => new(true, ReadReceiver() is not null);

    public void PreparePayment(Order order, Payment payment)
    {
        var receiver = ReadReceiver() ?? throw new ApiException(503,
            "Bank QR is not configured. Choose cash on delivery.", "Bank QR unavailable");
        if (order.GrandTotal <= 0 || order.GrandTotal != decimal.Truncate(order.GrandTotal)
            || order.GrandTotal >= 10000000000000m)
            throw new ApiException(409, "The order total is not a supported whole-VND QR amount.", "Invalid QR amount");

        payment.TransactionCode = "PM" + order.OrderId[..20].ToUpperInvariant();
        payment.BankBin = receiver.BankBin;
        payment.BankName = receiver.BankName;
        payment.BankAccountNo = receiver.AccountNo;
        payment.BankAccountName = receiver.AccountName;
    }

    public async Task<BankQrInstructionsResponse> GetInstructionsAsync(string userId, string orderId, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking()
            .SingleOrDefaultAsync(x => x.OrderId == orderId && x.UserId == userId, ct)
            ?? throw new ApiException(404, "Order was not found.", "Not found");
        if (order.PaymentMethod != "BANK_QR")
            throw new ApiException(409, "This order does not use bank QR.", "Payment method mismatch");
        if (order.OrderStatus == "CANCELLED")
            throw new ApiException(409, "This order has been cancelled. Do not transfer money.", "Order cancelled");
        var payment = await db.Payments.AsNoTracking().SingleAsync(x => x.OrderId == orderId, ct);
        if (payment.BankBin is null || payment.BankName is null || payment.BankAccountNo is null
            || payment.BankAccountName is null || payment.TransactionCode is null)
            throw new ApiException(409, "Bank QR information is missing from this order.", "Payment data missing");

        var receivedAmount = await db.BankQrWebhookEvents.AsNoTracking()
            .Where(x => x.OrderId == orderId && (x.Status == "PARTIAL" || x.Status == "APPLIED"))
            .SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
        var remainingAmount = Math.Max(0m, payment.Amount - receivedAmount);
        var imageUrl = $"https://img.vietqr.io/image/{payment.BankBin}-{payment.BankAccountNo}-compact2.png"
            + $"?amount={remainingAmount.ToString("0", CultureInfo.InvariantCulture)}"
            + $"&addInfo={Uri.EscapeDataString(payment.TransactionCode)}"
            + $"&accountName={Uri.EscapeDataString(payment.BankAccountName)}";
        return new BankQrInstructionsResponse(orderId, payment.Amount, payment.BankName,
            payment.BankAccountNo, payment.BankAccountName, payment.TransactionCode,
            imageUrl, payment.PaymentStatus, order.OrderStatus, receivedAmount);
    }

    public async Task ConfirmReceiptAsync(string adminUserId, string orderId, ConfirmPaymentRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var order = await db.Orders.SingleOrDefaultAsync(x => x.OrderId == orderId, ct)
            ?? throw new ApiException(404, "Order was not found.", "Not found");
        var payment = await db.Payments.SingleOrDefaultAsync(x => x.OrderId == orderId, ct)
            ?? throw new ApiException(404, "Payment was not found.", "Not found");
        if (order.OrderStatus == "CANCELLED" || payment.PaymentStatus != "PENDING")
            throw new ApiException(409, "This payment cannot be confirmed in its current state.", "Payment state conflict");
        if (request.ReceivedAmount != payment.Amount)
            throw new ApiException(409, "Received amount must match the order total exactly.", "Payment amount mismatch");

        string source;
        if (order.PaymentMethod == "BANK_QR")
        {
            var bankTransactionId = request.BankTransactionId?.Trim();
            if (string.IsNullOrWhiteSpace(bankTransactionId) || bankTransactionId.Length < 6)
                throw new ApiException(400, "Enter the bank transaction reference from the bank statement.", "Bank reference required");
            if (await db.Payments.AnyAsync(x => x.PaymentMethod == "BANK_QR"
                && x.ProviderTransactionId == bankTransactionId && x.OrderId != orderId, ct))
                throw new ApiException(409, "This bank transaction was used for another order.", "Duplicate bank transaction");
            payment.ProviderTransactionId = bankTransactionId;
            source = "ADMIN_MANUAL_BANK_RECONCILIATION";
        }
        else if (order.PaymentMethod == "COD")
        {
            if (order.OrderStatus != "DELIVERED")
                throw new ApiException(409, "Confirm cash collection only after delivery.", "Order not delivered");
            source = "ADMIN_CASH_COLLECTION";
        }
        else
            throw new ApiException(409, "This payment method is not supported here.", "Payment method mismatch");

        var now = DateTime.UtcNow;
        payment.PaymentStatus = "SUCCESS";
        payment.PaidAt = now;
        payment.UpdatedAt = now;
        payment.GatewayResponse = JsonSerializer.Serialize(new { source, adminUserId, confirmedAt = now });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private Receiver? ReadReceiver()
    {
        var bin = configuration["BankQr:BankBin"]?.Trim();
        var name = configuration["BankQr:BankName"]?.Trim();
        var account = configuration["BankQr:AccountNo"]?.Trim();
        var holder = configuration["BankQr:AccountName"]?.Trim();
        if (string.IsNullOrWhiteSpace(bin) || string.IsNullOrWhiteSpace(name)
            || string.IsNullOrWhiteSpace(account) || string.IsNullOrWhiteSpace(holder)) return null;
        if (!Regex.IsMatch(bin, "^[0-9]{6}$") || !Regex.IsMatch(account, "^[0-9]{6,19}$")
            || name.Length > 100 || holder.Length > 100)
            throw new InvalidOperationException("BankQr configuration is invalid.");
        return new Receiver(bin, name, account, holder);
    }
}
