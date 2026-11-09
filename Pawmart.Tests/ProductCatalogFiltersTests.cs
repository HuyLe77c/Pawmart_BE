using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Pawmart.Data;
using Pawmart.Models.Contracts.Catalog;
using Pawmart.Models.Entities;
using Pawmart.Services;

namespace Pawmart.Tests;

public sealed class ProductCatalogFiltersTests
{
    private static IQueryable<Product> Products => new[]
    {
        new Product { ProductId = 1, CategoryId = 1, ProductName = "Poodle adult", TargetPetType = "DOG", Brand = "Royal", Price = 200000, StockQuantity = 4, IsActive = true },
        new Product { ProductId = 2, CategoryId = 1, ProductName = "Food", Description = "For Poodle", TargetPetType = "DOG", Brand = "Royal", Price = 500000, StockQuantity = 0, IsActive = true },
        new Product { ProductId = 3, CategoryId = 2, ProductName = "Cat food", TargetPetType = "CAT", Brand = "Royal", Price = 100000, StockQuantity = 4, IsActive = true },
        new Product { ProductId = 4, CategoryId = 3, ProductName = "Bowl", TargetPetType = "ALL", Price = 80000, StockQuantity = 3, IsActive = true },
        new Product { ProductId = 5, CategoryId = 1, ProductName = "Poodle preview", TargetPetType = "DOG", Price = 0, StockQuantity = 3, IsActive = false },
    }.AsQueryable();

    [Fact]
    public void CombinedFiltersApplyBeforeCountAndPagination()
    {
        var filtered = ProductCatalogFilters.Apply(Products, new ProductQuery
        {
            PetType = "DOG", Breed = " pOoDlE ", CategoryId = 1, Brand = " royal ",
            MinPrice = 200000, MaxPrice = 500000, InStock = true, Search = "adult"
        });
        Assert.Equal(1, filtered.Count());
        Assert.Equal(1, Assert.Single(filtered.Skip(0).Take(1)).ProductId);
    }

    [Fact]
    public void AllPetsIncludesBothSpeciesAndSharedProducts()
    {
        Assert.Equal(5, ProductCatalogFilters.Apply(Products, new ProductQuery { PetType = "ALL" }).Count());
        Assert.Equal(new[] { 3, 4 }, ProductCatalogFilters.Apply(Products, new ProductQuery { PetType = "CAT" }).Select(x => x.ProductId));
    }

    [Fact]
    public void BreedMatchesNameOrDescriptionAndPriceBoundsAreInclusive()
    {
        var results = ProductCatalogFilters.Apply(Products, new ProductQuery { Breed = "poodle", MinPrice = 200000, MaxPrice = 500000 });
        Assert.Equal(new[] { 1, 2 }, results.Select(x => x.ProductId));
        Assert.Empty(ProductCatalogFilters.Apply(Products, new ProductQuery { Breed = "Corgi" }));
    }

    [Theory]
    [InlineData(-1, 100)]
    [InlineData(200, 100)]
    public void InvalidPriceRangesAreRejected(int minimum, int maximum)
    {
        var query = new ProductQuery { MinPrice = minimum, MaxPrice = maximum };
        Assert.False(Validator.TryValidateObject(query, new ValidationContext(query), [], true));
    }

    [Fact]
    public void CombinedQueryTranslatesToSqlServer()
    {
        using var db = new PawmartDbContext(new DbContextOptionsBuilder<PawmartDbContext>()
            .UseSqlServer("Server=localhost;Database=unused;Trusted_Connection=True;TrustServerCertificate=True").Options);
        var sql = ProductCatalogFilters.Apply(db.Products, new ProductQuery
        {
            PetType = "DOG", Breed = "Poodle", Brand = "Royal", CategoryId = 1,
            MinPrice = 100000, MaxPrice = 500000, InStock = true
        }).ToQueryString();
        Assert.Contains("WHERE", sql);
        Assert.Contains("[price]", sql);
        Assert.Contains("[description]", sql);
        Assert.Contains("[stock_quantity]", sql);
    }
}
