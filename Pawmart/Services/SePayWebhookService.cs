using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Pawmart.Data;
using Pawmart.Models.Entities;

namespace Pawmart.Services;

public sealed class SePayWebhookPayload
{
    public long Id { get; init; }
    public string? Gateway { get; init; }
    public string? AccountNumber { get; init; }
    public string? Code { get; init; }
    public string? Content { get; init; }
    public string? TransferType { get; init; }
    public decimal TransferAmount { get; init; }
    public string? ReferenceCode { get; init; }
}

public sealed record SePayWebhookResult(string Status, string? OrderId = null);

public sealed partial class SePayWebhookService(
    PawmartDbContext db,
    IConfiguration configuration,
    ILogger<SePayWebhookService> logger)
{
    [GeneratedRegex(@"(?<![A-Z0-9])PM[A-F0-9]{20}(?![A-Z0-9])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TransferCodePattern();

    public bool IsConfigured => configuration["SePay:WebhookApiKey"]?.Length >= 24;

    public bool IsAuthorized(string? authorization)
    {
        var key = configuration["SePay:WebhookApiKey"];
        if (string.IsNullOrWhiteSpace(key) || key.Length < 24 || authorization is null
            || !authorization.StartsWith("Apikey ", StringComparison.OrdinalIgnoreCase)) return false;
        var provided = authorization[7..].Trim();
        var expectedBytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        var providedBytes = SHA256.HashData(Encoding.UTF8.GetBytes(provided));
        return CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }

    public static string? ExtractTransferCode(string? code, string? content)
    {
        var matches = new[] { code, content }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .SelectMany(value => TransferCodePattern().Matches(value!).Select(match => match.Value.ToUpperInvariant()))
            .Distinct(StringComparer.Ordinal)
            .Take(2)
            .ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    public async Task<SePayWebhookResult> ProcessAsync(SePayWebhookPayload payload, CancellationToken ct)
    {
        if (payload.Id <= 0 || payload.TransferAmount <= 0 || payload.TransferAmount >= 10000000000000m
            || payload.TransferAmount != decimal.Truncate(payload.TransferAmount)
            || string.IsNullOrWhiteSpace(payload.AccountNumber) || payload.AccountNumber.Length > 30
            || payload.Gateway?.Length > 100 || payload.ReferenceCode?.Length > 150)
            return new SePayWebhookResult("INVALID");

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var previous = await db.BankQrWebhookEvents.AsNoTracking()
            .SingleOrDefaultAsync(x => x.ProviderEventId == payload.Id, ct);
        if (previous is not null) return new SePayWebhookResult("DUPLICATE", previous.OrderId);

        var transferCode = ExtractTransferCode(payload.Code, payload.Content);
        var gateway = payload.Gateway?.Trim().ToUpperInvariant();
        var reference = payload.ReferenceCode?.Trim().ToUpperInvariant();
        var webhookEvent = new BankQrWebhookEvent
        {
            ProviderEventId = payload.Id,
            TransferCode = transferCode,
            AccountNumber = payload.AccountNumber.Trim(),
            Gateway = gateway,
            ReferenceCode = reference,
            Amount = payload.TransferAmount,
            Status = "UNMATCHED",
            CreatedAt = DateTime.UtcNow
        };
        db.BankQrWebhookEvents.Add(webhookEvent);

        if (!string.Equals(payload.TransferType, "in", StringComparison.OrdinalIgnoreCase))
            webhookEvent.Status = "OUTGOING";
        else if (!string.IsNullOrEmpty(gateway) && !string.IsNullOrEmpty(reference)
            && await db.BankQrWebhookEvents.AnyAsync(x => x.Gateway == gateway && x.ReferenceCode == reference, ct))
            webhookEvent.Status = "DUPLICATE_REFERENCE";
        else if (transferCode is not null)
        {
            var payment = await db.Payments.SingleOrDefaultAsync(x => x.PaymentMethod == "BANK_QR"
                && x.TransactionCode == transferCode, ct);
            if (payment is not null)
            {
                webhookEvent.OrderId = payment.OrderId;
                var order = await db.Orders.SingleAsync(x => x.OrderId == payment.OrderId, ct);
                if (!string.Equals(payment.BankAccountNo, webhookEvent.AccountNumber, StringComparison.Ordinal))
                    webhookEvent.Status = "ACCOUNT_MISMATCH";
                else if (order.OrderStatus == "CANCELLED" || payment.PaymentStatus == "CANCELLED")
                    webhookEvent.Status = "CANCELLED_ORDER";
                else if (payment.PaymentStatus == "SUCCESS")
                    webhookEvent.Status = "ALREADY_PAID";
                else if (payment.PaymentStatus != "PENDING")
                    webhookEvent.Status = "PAYMENT_CONFLICT";
                else
                {
                    var received = await db.BankQrWebhookEvents
                        .Where(x => x.OrderId == payment.OrderId && (x.Status == "PARTIAL" || x.Status == "APPLIED"))
                        .SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
                    received += payload.TransferAmount;
                    if (received < payment.Amount)
                        webhookEvent.Status = "PARTIAL";
                    else
                    {
                        webhookEvent.Status = "APPLIED";
                        var now = DateTime.UtcNow;
                        payment.PaymentStatus = "SUCCESS";
                        payment.PaidAt = now;
                        payment.UpdatedAt = now;
                        payment.ProviderTransactionId = $"SEPAY:{payload.Id}";
                        payment.GatewayResponse = JsonSerializer.Serialize(new
                        {
                            source = "SEPAY_WEBHOOK", providerEventId = payload.Id,
                            receivedAmount = received, confirmedAt = now
                        });
                        db.Notifications.Add(new Notification
                        {
                            UserId = order.UserId,
                            Title = "Đã nhận thanh toán",
                            Message = "PawMart đã nhận thanh toán cho đơn #" + order.OrderId[..8].ToUpperInvariant() + ".",
                            NotificationType = "ORDER",
                            ReferenceId = order.OrderId,
                            CreatedAt = now
                        });
                    }
                }
            }
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        logger.LogInformation("SePay event {EventId} reconciled as {Status} for order {OrderId}",
            payload.Id, webhookEvent.Status, webhookEvent.OrderId);
        return new SePayWebhookResult(webhookEvent.Status, webhookEvent.OrderId);
    }
}
