using System.ComponentModel.DataAnnotations;
using Pawmart.Models.Contracts.Catalog;

namespace Pawmart.Models.Contracts.Cart;

public sealed class CartItemRequest
{
    [Range(1, int.MaxValue)] public int ProductId { get; init; }
    [Range(1, 10000)] public int Quantity { get; init; }
}

public sealed record CartLineResponse(int ProductId, string ProductName, string? ImageUrl, decimal UnitPrice, int Quantity, decimal LineTotal, int AvailableStock);
public sealed record CartResponse(IReadOnlyList<CartLineResponse> Items, decimal Subtotal, int TotalQuantity);
