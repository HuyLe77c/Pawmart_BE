using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Pawmart.Data;
using Pawmart.Models.Entities;
using Pawmart.Services;

namespace Pawmart.Tests;

public sealed class SePayWebhookTests
{
    private const string OrderCode = "PM0123456789ABCDEF0123";

    [Theory]
    [InlineData("PM0123456789ABCDEF0123", null, OrderCode)]
    [InlineData(null, "MB thanh toan PM0123456789ABCDEF0123 don hang", OrderCode)]
    [InlineData("PM0123456789ABCDEF0123", "PM0123456789ABCDEF0123", OrderCode)]
    [InlineData(null, "XPM0123456789ABCDEF0123", null)]
    [InlineData(null, "PM0123456789ABCDEF0123A", null)]
    [InlineData("PM0123456789ABCDEF0123", "PMABCDEF01234567890123", null)]
    public void TransferCodeRequiresOneExactUnambiguousToken(string? code, string? content, string? expected)
    {
        Assert.Equal(expected, SePayWebhookService.ExtractTransferCode(code, content));
    }

    [Fact]
    public void WebhookRejectsMissingAndWrongAuthentication()
    {
        var unconfigured = CreateService(null);
        Assert.False(unconfigured.IsConfigured);
        Assert.False(unconfigured.IsAuthorized("Apikey any-value"));
        Assert.False(CreateService("short-key").IsConfigured);

        var service = CreateService("qa-only-secret-with-enough-length");
        Assert.True(service.IsConfigured);
        Assert.False(service.IsAuthorized(null));
        Assert.False(service.IsAuthorized("Bearer qa-only-secret-with-enough-length"));
        Assert.False(service.IsAuthorized("Apikey wrong-secret-with-enough-length"));
        Assert.True(service.IsAuthorized("Apikey qa-only-secret-with-enough-length"));
    }

    [Fact]
    public void WebhookEventHasStableSqlIdentity()
    {
        var options = new DbContextOptionsBuilder<PawmartDbContext>()
            .UseSqlServer("Server=localhost;Database=unused;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var db = new PawmartDbContext(options);
        var entity = db.Model.FindEntityType(typeof(BankQrWebhookEvent));
        Assert.NotNull(entity);
        Assert.Equal("BankQrWebhookEvents", entity.GetTableName());
        Assert.Equal(nameof(BankQrWebhookEvent.ProviderEventId),
            Assert.Single(entity.FindPrimaryKey()!.Properties).Name);
    }

    private static SePayWebhookService CreateService(string? key)
    {
        var values = new Dictionary<string, string?> { ["SePay:WebhookApiKey"] = key };
        var config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return new SePayWebhookService(null!, config, NullLogger<SePayWebhookService>.Instance);
    }
}
