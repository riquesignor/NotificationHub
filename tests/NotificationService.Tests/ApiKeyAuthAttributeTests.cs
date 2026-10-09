using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NotificationService.API.Security;
using NotificationService.Application.Security;
using Xunit;

namespace NotificationService.Tests;

public class ApiKeyAuthAttributeTests
{
    private static AuthorizationFilterContext Context(string? apiKey, IClientAppRegistry registry)
    {
        var services = new ServiceCollection().AddSingleton(registry).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        if (apiKey is not null)
        {
            http.Request.Headers[ApiKeyAuthAttribute.HeaderName] = apiKey;
        }
        var action = new ActionContext(http, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(action, new List<IFilterMetadata>());
    }

    private static readonly IClientAppRegistry Registry = new ClientAppRegistry(new[]
    {
        new ClientAppDefinition { Id = "weather", PublicKey = "pub", SecretKey = "sec" }
    });

    [Theory]
    [InlineData(null)]
    [InlineData("pub")]
    [InlineData("nope")]
    public void SecretRoute_RejectsMissingWrongOrPublicKey(string? key)
    {
        var context = Context(key, Registry);

        new ApiKeyAuthAttribute(ApiKeyScope.Secret).OnAuthorization(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
    }

    [Fact]
    public void PublicRoute_AcceptsPublicKey_AndExposesClientApp()
    {
        var context = Context("pub", Registry);

        new ApiKeyAuthAttribute(ApiKeyScope.Public).OnAuthorization(context);

        Assert.Null(context.Result);
        Assert.Equal("weather", context.HttpContext.GetClientApp().Id);
    }

    [Fact]
    public void LegacyMode_AllowsRequestsWithoutKey()
    {
        var context = Context(null, new ClientAppRegistry(Array.Empty<ClientAppDefinition>()));

        new ApiKeyAuthAttribute(ApiKeyScope.Secret).OnAuthorization(context);

        Assert.Null(context.Result);
        Assert.Equal(ClientAppRegistry.LegacyAppId, context.HttpContext.GetClientApp().Id);
    }
}
