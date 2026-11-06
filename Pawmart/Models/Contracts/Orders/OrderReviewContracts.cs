using System.ComponentModel.DataAnnotations;

namespace Pawmart.Models.Contracts.Orders;

public sealed class CreateOrderReviewRequest
{
    [Range(1, 5)] public int Rating { get; init; }
    [StringLength(600)] public string? Comment { get; init; }
    public bool SharePublicly { get; init; }
}

public sealed class ReplyOrderReviewRequest
{
    [StringLength(600)] public string? Reply { get; init; }
}

public sealed class SetOrderReviewVisibilityRequest
{
    public bool IsVisible { get; init; }
}

public sealed record OrderReviewResponse(int ReviewId, string OrderId, string AuthorName,
    int Rating, string? Comment, bool SharePublicly, bool IsVisible,
    string? ShopReply, DateTime CreatedAt, DateTime? RepliedAt);

public sealed record PublicOrderReviewResponse(string AuthorName, int Rating,
    string? Comment, string? ShopReply, DateTime CreatedAt);

public sealed record PublicOrderReviewsResponse(int Total, decimal AverageRating,
    IReadOnlyList<PublicOrderReviewResponse> Items);
