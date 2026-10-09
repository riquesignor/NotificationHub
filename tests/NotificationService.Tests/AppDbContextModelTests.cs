using Microsoft.EntityFrameworkCore;
using NotificationService.Infrastructure.Data;
using Xunit;

namespace NotificationService.Tests;

// Valida o modelo do EF sem banco: gerar o script de criação já monta (e
// valida) o modelo inteiro com o provider do Postgres.
public class AppDbContextModelTests
{
    private static string CreateScript()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=unused")
            .Options;
        using var context = new AppDbContext(options);
        return context.Database.GenerateCreateScript();
    }

    [Theory]
    [InlineData("IX_DeviceTokens_AppId_Token")]
    [InlineData("IX_Notifications_AppId_IdempotencyKey")]
    [InlineData("IX_NotificationTemplates_AppId_Name")]
    public void UniqueIndexes_AreCreated(string indexName)
    {
        var script = CreateScript();

        Assert.Contains($"CREATE UNIQUE INDEX \"{indexName}\"", script);
    }

    [Fact]
    public void DeviceAttributes_AreStoredAsText()
    {
        var script = CreateScript();

        Assert.Contains("\"Attributes\" text NOT NULL", script);
    }
}
