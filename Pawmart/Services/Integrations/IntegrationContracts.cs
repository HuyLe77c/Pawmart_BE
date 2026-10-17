using Pawmart.Models.Entities;

namespace Pawmart.Services.Integrations;

public sealed record PaymentStartResult(string RedirectUrl, string PaymentReference);
public sealed record VerifiedPaymentCallback(string OrderId, decimal Amount, bool Succeeded, string ProviderTransactionId, string? TransactionCode);
public sealed record ShippingQuote(decimal DistanceKm, decimal Fee);
public sealed record AiProductCandidate(int ProductId, string Name, string TargetPetType, string? Description, string? Ingredients, decimal Price);
public sealed record AiRecommendationRequest(string PetType, string? Breed, int? AgeMonths, decimal? WeightKg, string? SpecialNotes, IReadOnlyList<AiProductCandidate> Products);
public sealed record AiRecommendationResponse(string Advice, IReadOnlyList<int> RecommendedProductIds);
public sealed record PushDeliveryResult(bool Available, int SentCount, string? Detail);

public interface IPaymentGateway
{
    /// <summary>Must use the stable key on every retry and return the same reference/redirect for that key.</summary>
    Task<PaymentStartResult> InitiateAsync(Order order, Payment payment, string idempotencyKey, CancellationToken ct);
    Task<VerifiedPaymentCallback> VerifyCallbackAsync(IReadOnlyDictionary<string, string> values, CancellationToken ct);
}

public interface IShippingCalculator
{
    Task<ShippingQuote> GetQuoteAsync(decimal fromLatitude, decimal fromLongitude, decimal toLatitude, decimal toLongitude, CancellationToken ct);
}

public interface IPushNotificationSender
{
    Task<PushDeliveryResult> SendAsync(IReadOnlyList<string> deviceTokens, string title, string message, CancellationToken ct);
}

public interface IAiRecommendationService
{
    Task<AiRecommendationResponse> RecommendAsync(AiRecommendationRequest request, CancellationToken ct);
}
