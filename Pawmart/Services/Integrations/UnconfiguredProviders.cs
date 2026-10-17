using Pawmart.Middleware;
using Pawmart.Models.Entities;

namespace Pawmart.Services.Integrations;

public sealed class UnconfiguredPaymentGateway : IPaymentGateway
{
    public Task<PaymentStartResult> InitiateAsync(Order order, Payment payment, string idempotencyKey, CancellationToken ct) =>
        throw Unavailable("Payment gateway");
    public Task<VerifiedPaymentCallback> VerifyCallbackAsync(IReadOnlyDictionary<string, string> values, CancellationToken ct) =>
        throw Unavailable("Payment gateway");
    private static ApiException Unavailable(string provider) => new(503, $"{provider} is not configured.", "Payment provider unavailable");
}

public sealed class UnconfiguredShippingCalculator : IShippingCalculator
{
    public Task<ShippingQuote> GetQuoteAsync(decimal fromLatitude, decimal fromLongitude, decimal toLatitude, decimal toLongitude, CancellationToken ct) =>
        throw new ApiException(503, "A distance provider is not configured. Checkout can still use the configured flat shipping fee.", "Distance provider unavailable");
}

public sealed class UnconfiguredPushNotificationSender : IPushNotificationSender
{
    public Task<PushDeliveryResult> SendAsync(IReadOnlyList<string> deviceTokens, string title, string message, CancellationToken ct) =>
        Task.FromResult(new PushDeliveryResult(false, 0, "Firebase Cloud Messaging is not configured."));
}

public sealed class UnconfiguredAiRecommendationService : IAiRecommendationService
{
    public Task<AiRecommendationResponse> RecommendAsync(AiRecommendationRequest request, CancellationToken ct) =>
        throw new ApiException(503, "An AI provider is not configured.", "AI provider unavailable");
}
