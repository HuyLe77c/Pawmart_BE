using System.ComponentModel.DataAnnotations;

namespace Pawmart.Models.Contracts.Commerce;

public sealed class ChatTurn
{
    public string Role { get; init; } = "";
    public string Content { get; init; } = "";
}

public sealed class ChatMessageRequest
{
    [Required, StringLength(2000, MinimumLength = 1)]
    public string Message { get; init; } = "";
    public List<ChatTurn> History { get; init; } = [];
}

public sealed record ChatProductLink(int ProductId, string ProductName, decimal Price);

public sealed record ChatMessageResponse(string Answer)
{
    public IReadOnlyList<ChatProductLink> Products { get; init; } = [];
}
