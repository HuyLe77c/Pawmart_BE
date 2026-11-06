using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Pawmart.Models.Entities;

namespace Pawmart.Data;

public sealed class PawmartDbContext(DbContextOptions<PawmartDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Pet> Pets => Set<Pet>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductCatalogReview> ProductCatalogReviews => Set<ProductCatalogReview>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderStatusHistory> OrderStatusHistory => Set<OrderStatusHistory>();
    public DbSet<OrderReview> OrderReviews => Set<OrderReview>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        MapSnakeCaseColumns(modelBuilder);
        ConfigureUsers(modelBuilder);
        ConfigurePets(modelBuilder);
        ConfigureCategories(modelBuilder);
        ConfigureProducts(modelBuilder);
        ConfigureProductImages(modelBuilder);
        ConfigureProductCatalogReviews(modelBuilder);
        ConfigureCart(modelBuilder);
        ConfigureFavorites(modelBuilder);
        ConfigureStores(modelBuilder);
        ConfigurePromotions(modelBuilder);
        ConfigureOrders(modelBuilder);
        ConfigureOrderItems(modelBuilder);
        ConfigureOrderStatusHistory(modelBuilder);
        ConfigureOrderReviews(modelBuilder);
        ConfigurePayments(modelBuilder);
        ConfigureNotifications(modelBuilder);
    }

    private static void MapSnakeCaseColumns(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        foreach (var property in entity.GetProperties())
            property.SetColumnName(ToSnakeCase(property.Name));
    }

    private static string ToSnakeCase(string value) =>
        Regex.Replace(value, "(?<!^)([A-Z])", "_$1").ToLowerInvariant();

    private static void ConfigureUsers(ModelBuilder modelBuilder)
    {
        var e = modelBuilder.Entity<User>();
        e.ToTable("Users");
        e.HasKey(x => x.UserId);
        e.Property(x => x.UserId).HasMaxLength(50).IsUnicode(false);
        e.Property(x => x.Email).HasMaxLength(150).IsRequired();
        e.Property(x => x.PasswordHash).HasMaxLength(255).IsRequired();
        e.Property(x => x.FullName).HasMaxLength(100).IsRequired();
        e.Property(x => x.PhoneNumber).HasMaxLength(20).IsUnicode(false);
        e.Property(x => x.Role).HasMaxLength(20).IsUnicode(false).HasDefaultValue("CUSTOMER");
        e.Property(x => x.FcmToken).HasMaxLength(1000);
        e.Property(x => x.IsActive).HasDefaultValue(true);
        e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasIndex(x => x.Email).IsUnique();
        e.HasIndex(x => new { x.Role, x.IsActive });
    }

    private static void ConfigurePets(ModelBuilder modelBuilder)
    {
        var e = modelBuilder.Entity<Pet>();
        e.ToTable("Pets");
        e.HasKey(x => x.PetId);
        e.Property(x => x.PetName).HasMaxLength(50).IsRequired();
        e.Property(x => x.PetType).HasMaxLength(20).IsUnicode(false).IsRequired();
        e.Property(x => x.Breed).HasMaxLength(100);
        e.Property(x => x.WeightKg).HasPrecision(5, 2);
        e.Property(x => x.BodySize).HasMaxLength(10).IsUnicode(false);
        e.Property(x => x.ActivityLevel).HasMaxLength(10).IsUnicode(false);
        e.Property(x => x.FeedingHabits).HasMaxLength(500);
        e.Property(x => x.FoodPreferences).HasMaxLength(500);
        e.Property(x => x.ImageUrl).HasMaxLength(1000);
        e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => x.UserId);
    }

    private static void ConfigureCategories(ModelBuilder modelBuilder)
    {
        var e = modelBuilder.Entity<Category>();
        e.ToTable("Categories");
        e.HasKey(x => x.CategoryId);
        e.Property(x => x.CategoryName).HasMaxLength(100).IsRequired();
        e.Property(x => x.ImageUrl).HasMaxLength(1000);
        e.Property(x => x.Description).HasMaxLength(500);
        e.Property(x => x.SortOrder).HasDefaultValue(0);
        e.Property(x => x.IsActive).HasDefaultValue(true);
        e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasIndex(x => x.CategoryName).IsUnique();
        e.HasIndex(x => new { x.IsActive, x.SortOrder });
    }

    private static void ConfigureProducts(ModelBuilder modelBuilder)
    {
        var e = modelBuilder.Entity<Product>();
        e.ToTable("Products");
        e.HasKey(x => x.ProductId);
        e.Property(x => x.Sku).HasMaxLength(50).IsUnicode(false);
        e.Property(x => x.ProductName).HasMaxLength(150).IsRequired();
        e.Property(x => x.Brand).HasMaxLength(100);
        e.Property(x => x.PackageSize).HasMaxLength(100);
        e.Property(x => x.LifeStage).HasMaxLength(100);
        e.Property(x => x.Price).HasPrecision(18, 2);
        e.Property(x => x.StockQuantity).HasDefaultValue(0);
        e.Property(x => x.LowStockThreshold).HasDefaultValue(5);
        e.Property(x => x.TargetPetType).HasMaxLength(20).IsUnicode(false).HasDefaultValue("ALL");
        e.Property(x => x.IsActive).HasDefaultValue(true);
        e.Property(x => x.IsListed).HasDefaultValue(true);
        e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasOne<Category>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.NoAction);
        e.HasIndex(x => x.Sku).IsUnique().HasFilter("[sku] IS NOT NULL");
        e.HasIndex(x => new { x.CategoryId, x.IsActive });
        e.HasIndex(x => new { x.TargetPetType, x.IsActive });
        e.HasIndex(x => new { x.IsFeatured, x.IsActive });
    }

    private static void ConfigureProductImages(ModelBuilder modelBuilder)
    {
        var e = modelBuilder.Entity<ProductImage>();
        e.ToTable("Product_Images");
        e.HasKey(x => x.ImageId);
        e.Property(x => x.ImageUrl).HasMaxLength(1000).IsRequired();
        e.Property(x => x.IsPrimary).HasDefaultValue(false);
        e.Property(x => x.DisplayOrder).HasDefaultValue(0);
        e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => x.ProductId).IsUnique().HasFilter("[is_primary] = 1");
        e.HasIndex(x => new { x.ProductId, x.DisplayOrder }).IsUnique();
    }

    private static void ConfigureProductCatalogReviews(ModelBuilder modelBuilder)
    {
        var e = modelBuilder.Entity<ProductCatalogReview>();
        e.ToTable("Catalog_Product_Reviews");
        e.HasKey(x => x.ProductId);
        e.Property(x => x.Slug).HasMaxLength(180).IsUnicode(false).IsRequired();
        e.Property(x => x.ModelName).HasMaxLength(150).IsRequired();
        e.Property(x => x.ShortDescription).HasMaxLength(500);
        e.Property(x => x.OfficialUrl).HasMaxLength(1000).IsRequired();
        e.Property(x => x.ModelStatus).HasMaxLength(20).IsUnicode(false).IsRequired();
        e.Property(x => x.ImageSourceUrl).HasMaxLength(1000);
        e.Property(x => x.ImageStatus).HasMaxLength(20).IsUnicode(false).IsRequired();
        e.Property(x => x.PriceSourceUrl).HasMaxLength(1000);
        e.Property(x => x.PriceStatus).HasMaxLength(20).IsUnicode(false).IsRequired();
        e.Property(x => x.ProductStatus).HasMaxLength(20).IsUnicode(false).IsRequired();
        e.Property(x => x.PublicPreview).HasDefaultValue(false);
        e.Property(x => x.ReviewNote).HasMaxLength(1000);
        e.HasOne<Product>().WithOne().HasForeignKey<ProductCatalogReview>(x => x.ProductId).OnDelete(DeleteBehavior.NoAction);
        e.HasIndex(x => x.Slug).IsUnique();
    }

    private static void ConfigureCart(ModelBuilder modelBuilder)
    {
        var e = modelBuilder.Entity<CartItem>();
        e.ToTable("CartItems");
        e.HasKey(x => x.CartItemId);
        e.Property(x => x.Quantity).HasDefaultValue(1);
        e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.NoAction);
        e.HasIndex(x => new { x.UserId, x.ProductId }).IsUnique();
    }

    private static void ConfigureFavorites(ModelBuilder modelBuilder)
    {
        var e = modelBuilder.Entity<Favorite>();
        e.ToTable("Favorites");
        e.HasKey(x => x.FavoriteId);
        e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => new { x.UserId, x.ProductId }).IsUnique();
    }

    private static void ConfigureStores(ModelBuilder modelBuilder)
    {
        var e = modelBuilder.Entity<Store>();
        e.ToTable("Stores");
        e.HasKey(x => x.StoreId);
        e.Property(x => x.StoreName).HasMaxLength(150).IsRequired();
        e.Property(x => x.StoreType).HasMaxLength(20).IsUnicode(false).HasDefaultValue("STORE");
        e.Property(x => x.Address).HasMaxLength(500).IsRequired();
        e.Property(x => x.Latitude).HasPrecision(10, 7);
        e.Property(x => x.Longitude).HasPrecision(10, 7);
        e.Property(x => x.PhoneNumber).HasMaxLength(20).IsUnicode(false);
        e.Property(x => x.IsActive).HasDefaultValue(true);
        e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
    }

    private static void ConfigurePromotions(ModelBuilder modelBuilder)
    {
        var e = modelBuilder.Entity<Promotion>();
        e.ToTable("Promotions");
        e.HasKey(x => x.PromotionId);
        e.Property(x => x.Code).HasMaxLength(50).IsUnicode(false).IsRequired();
        e.Property(x => x.PromotionName).HasMaxLength(150).IsRequired();
        e.Property(x => x.Description).HasMaxLength(500);
        e.Property(x => x.DiscountType).HasMaxLength(20).IsUnicode(false).IsRequired();
        e.Property(x => x.DiscountValue).HasPrecision(18, 2);
        e.Property(x => x.MinOrderAmount).HasPrecision(18, 2).HasDefaultValue(0m);
        e.Property(x => x.MaxDiscountAmount).HasPrecision(18, 2);
        e.Property(x => x.UsedCount).HasDefaultValue(0);
        e.Property(x => x.IsActive).HasDefaultValue(true);
        e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasIndex(x => x.Code).IsUnique();
        e.HasIndex(x => new { x.IsActive, x.StartAt, x.EndAt });
    }

    private static void ConfigureOrders(ModelBuilder modelBuilder)
    {
        var e = modelBuilder.Entity<Order>();
        e.ToTable("Orders", table => table.UseSqlOutputClause(false));
        e.HasKey(x => x.OrderId);
        e.Property(x => x.OrderId).HasMaxLength(50).IsUnicode(false);
        e.Property(x => x.SubtotalAmount).HasPrecision(18, 2);
        e.Property(x => x.DiscountAmount).HasPrecision(18, 2).HasDefaultValue(0m);
        e.Property(x => x.ShippingFee).HasPrecision(18, 2).HasDefaultValue(0m);
        e.Property(x => x.GrandTotal).HasPrecision(18, 2);
        e.Property(x => x.PromotionCodeSnapshot).HasMaxLength(50).IsUnicode(false);
        e.Property(x => x.ShippingAddress).HasMaxLength(500).IsRequired();
        e.Property(x => x.RecipientName).HasMaxLength(100).IsRequired();
        e.Property(x => x.RecipientPhone).HasMaxLength(20).IsUnicode(false).IsRequired();
        e.Property(x => x.Latitude).HasPrecision(10, 7);
        e.Property(x => x.Longitude).HasPrecision(10, 7);
        e.Property(x => x.DistanceKm).HasPrecision(10, 2);
        e.Property(x => x.PaymentMethod).HasMaxLength(30).IsUnicode(false).HasDefaultValue("COD");
        e.Property(x => x.OrderStatus).HasMaxLength(30).IsUnicode(false).HasDefaultValue("PENDING");
        e.Property(x => x.Note).HasMaxLength(500);
        e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        e.HasOne<Store>().WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.NoAction);
        e.HasOne<Promotion>().WithMany().HasForeignKey(x => x.PromotionId).OnDelete(DeleteBehavior.SetNull);
        e.HasIndex(x => new { x.UserId, x.CreatedAt });
        e.HasIndex(x => new { x.OrderStatus, x.CreatedAt });
    }

    private static void ConfigureOrderItems(ModelBuilder modelBuilder)
    {
        var e = modelBuilder.Entity<OrderItem>();
        e.ToTable("OrderItems");
        e.HasKey(x => x.OrderItemId);
        e.Property(x => x.OrderId).HasMaxLength(50).IsUnicode(false).IsRequired();
        e.Property(x => x.UnitPrice).HasPrecision(18, 2);
        e.Property(x => x.LineTotal).HasPrecision(29, 2).HasComputedColumnSql("([quantity] * [unit_price])", stored: true);
        e.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.NoAction);
    }

    private static void ConfigureOrderStatusHistory(ModelBuilder modelBuilder)
    {
        var e = modelBuilder.Entity<OrderStatusHistory>();
        e.ToTable("OrderStatusHistory");
        e.HasKey(x => x.HistoryId);
        e.Property(x => x.OrderId).HasMaxLength(50).IsUnicode(false).IsRequired();
        e.Property(x => x.Status).HasMaxLength(30).IsUnicode(false).IsRequired();
        e.Property(x => x.Note).HasMaxLength(500);
        e.Property(x => x.ChangedBy).HasMaxLength(50).IsUnicode(false);
        e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne<User>().WithMany().HasForeignKey(x => x.ChangedBy).OnDelete(DeleteBehavior.NoAction);
    }

    private static void ConfigureOrderReviews(ModelBuilder modelBuilder)
    {
        var e = modelBuilder.Entity<OrderReview>();
        e.ToTable("OrderReviews");
        e.HasKey(x => x.ReviewId);
        e.Property(x => x.OrderId).HasMaxLength(50).IsUnicode(false).IsRequired();
        e.Property(x => x.UserId).HasMaxLength(50).IsUnicode(false).IsRequired();
        e.Property(x => x.AuthorName).HasMaxLength(60).IsRequired();
        e.Property(x => x.Comment).HasMaxLength(600);
        e.Property(x => x.ShopReply).HasMaxLength(600);
        e.Property(x => x.IsVisible).HasDefaultValue(true);
        e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        e.HasIndex(x => x.OrderId).IsUnique();
        e.HasIndex(x => new { x.IsVisible, x.CreatedAt });
    }

    private static void ConfigurePayments(ModelBuilder modelBuilder)
    {
        var e = modelBuilder.Entity<Payment>();
        e.ToTable("Payments");
        e.HasKey(x => x.PaymentId);
        e.Property(x => x.PaymentId).HasMaxLength(50).IsUnicode(false);
        e.Property(x => x.OrderId).HasMaxLength(50).IsUnicode(false).IsRequired();
        e.Property(x => x.PaymentMethod).HasMaxLength(30).IsUnicode(false).IsRequired();
        e.Property(x => x.PaymentStatus).HasMaxLength(20).IsUnicode(false).HasDefaultValue("PENDING");
        e.Property(x => x.Amount).HasPrecision(18, 2);
        e.Property(x => x.TransactionCode).HasMaxLength(150).IsUnicode(false);
        e.Property(x => x.ProviderTransactionId).HasMaxLength(150).IsUnicode(false);
        e.Property(x => x.BankBin).HasMaxLength(6).IsUnicode(false);
        e.Property(x => x.BankName).HasMaxLength(100);
        e.Property(x => x.BankAccountNo).HasMaxLength(30).IsUnicode(false);
        e.Property(x => x.BankAccountName).HasMaxLength(100);
        e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => x.OrderId).IsUnique();
    }

    private static void ConfigureNotifications(ModelBuilder modelBuilder)
    {
        var e = modelBuilder.Entity<Notification>();
        e.ToTable("Notifications");
        e.HasKey(x => x.NotificationId);
        e.Property(x => x.Title).HasMaxLength(150).IsRequired();
        e.Property(x => x.Message).HasMaxLength(1000).IsRequired();
        e.Property(x => x.NotificationType).HasMaxLength(30).IsUnicode(false);
        e.Property(x => x.ReferenceId).HasMaxLength(50).IsUnicode(false);
        e.Property(x => x.IsRead).HasDefaultValue(false);
        e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAt });
    }
}
