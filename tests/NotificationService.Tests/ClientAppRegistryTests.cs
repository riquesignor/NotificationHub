using NotificationService.Application.Security;
using Xunit;

namespace NotificationService.Tests;

public class ClientAppRegistryTests
{
    private static ClientAppRegistry Registry() => new(new[]
    {
        new ClientAppDefinition { Id = "weather", PublicKey = "pub-weather", SecretKey = "sec-weather" },
        new ClientAppDefinition { Id = "chat", PublicKey = "pub-chat", SecretKey = "sec-chat" }
    });

    [Fact]
    public void WithoutApps_RunsInLegacyMode()
    {
        var registry = new ClientAppRegistry(Array.Empty<ClientAppDefinition>());

        Assert.True(registry.IsLegacyMode);
        Assert.Equal(ClientAppRegistry.LegacyAppId, registry.Authenticate(null, ApiKeyScope.Secret)?.Id);
    }

    [Fact]
    public void PublicKey_OnlyGrantsPublicScope()
    {
        var registry = Registry();

        Assert.Equal("weather", registry.Authenticate("pub-weather", ApiKeyScope.Public)?.Id);
        Assert.Null(registry.Authenticate("pub-weather", ApiKeyScope.Secret));
    }

    [Fact]
    public void SecretKey_GrantsBothScopes_ForItsOwnApp()
    {
        var registry = Registry();

        Assert.Equal("chat", registry.Authenticate("sec-chat", ApiKeyScope.Secret)?.Id);
        Assert.Equal("chat", registry.Authenticate("sec-chat", ApiKeyScope.Public)?.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("wrong")]
    public void UnknownOrMissingKey_IsRejected(string? key)
    {
        Assert.Null(Registry().Authenticate(key, ApiKeyScope.Public));
    }

    [Fact]
    public void SamePublicAndSecretKey_IsRejectedAtStartup()
    {
        Assert.Throws<InvalidOperationException>(() => new ClientAppRegistry(new[]
        {
            new ClientAppDefinition { Id = "x", PublicKey = "same", SecretKey = "same" }
        }));
    }

    [Fact]
    public void DuplicatedAppId_IsRejectedAtStartup()
    {
        Assert.Throws<InvalidOperationException>(() => new ClientAppRegistry(new[]
        {
            new ClientAppDefinition { Id = "x", PublicKey = "p1", SecretKey = "s1" },
            new ClientAppDefinition { Id = "x", PublicKey = "p2", SecretKey = "s2" }
        }));
    }
}
