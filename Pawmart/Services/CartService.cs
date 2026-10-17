using Microsoft.EntityFrameworkCore;
using Pawmart.Data;
using Pawmart.Middleware;
using Pawmart.Models.Contracts.Cart;
using Pawmart.Models.Entities;

namespace Pawmart.Services;

public sealed class CartService(PawmartDbContext db)
{
    public async Task<CartResponse> GetAsync(string userId, CancellationToken ct)
    {
        var rows = await (from item in db.CartItems.AsNoTracking()
                          join product in db.Products.AsNoTracking() on item.ProductId equals product.ProductId
                          where item.UserId == userId && product.IsActive
                          orderby item.CreatedAt
                          select new
                          {
                              product.ProductId,
                              product.ProductName,
                              Image = db.ProductImages.Where(i => i.ProductId == product.ProductId).OrderByDescending(i => i.IsPrimary).ThenBy(i => i.DisplayOrder).Select(i => i.ImageUrl).FirstOrDefault(),
                              product.Price,
                              item.Quantity,
                              product.StockQuantity
                          }).ToListAsync(ct);
        var lines = rows.Select(x => new CartLineResponse(x.ProductId, x.ProductName, x.Image, x.Price, x.Quantity, x.Price * x.Quantity, x.StockQuantity)).ToArray();
        return new CartResponse(lines, lines.Sum(x => x.LineTotal), lines.Sum(x => x.Quantity));
    }

    public async Task<CartResponse> UpsertItemAsync(string userId, int productId, int quantity, CancellationToken ct)
    {
        if (quantity <= 0) throw new ApiException(400, "Quantity must be greater than zero.", "Invalid quantity");
        var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(x => x.ProductId == productId && x.IsActive, ct)
            ?? throw new ApiException(404, "Product was not found.", "Not found");
        if (quantity > product.StockQuantity) throw new ApiException(409, "The requested quantity exceeds available stock.", "Insufficient stock");

        var item = await db.CartItems.SingleOrDefaultAsync(x => x.UserId == userId && x.ProductId == productId, ct);
        if (item is null)
        {
            db.CartItems.Add(new CartItem { UserId = userId, ProductId = productId, Quantity = quantity, CreatedAt = DateTime.UtcNow });
        }
        else
        {
            item.Quantity = quantity;
            item.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        return await GetAsync(userId, ct);
    }

    public async Task RemoveItemAsync(string userId, int productId, CancellationToken ct)
    {
        var item = await db.CartItems.SingleOrDefaultAsync(x => x.UserId == userId && x.ProductId == productId, ct)
            ?? throw new ApiException(404, "Cart item was not found.", "Not found");
        db.CartItems.Remove(item);
        await db.SaveChangesAsync(ct);
    }
}
