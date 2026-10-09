using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NotificationService.Application.Security;

namespace NotificationService.API.Security;

// Exige o header X-Api-Key com uma chave que dê acesso ao escopo pedido e
// disponibiliza a app autenticada via HttpContext.GetClientApp().
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class ApiKeyAuthAttribute : Attribute, IAuthorizationFilter
{
    public const string HeaderName = "X-Api-Key";
    private const string ItemKey = "NotificationHub.ClientApp";

    public ApiKeyAuthAttribute(ApiKeyScope scope)
    {
        Scope = scope;
    }

    public ApiKeyScope Scope { get; }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var registry = context.HttpContext.RequestServices.GetRequiredService<IClientAppRegistry>();
        var apiKey = context.HttpContext.Request.Headers[HeaderName].FirstOrDefault();

        var app = registry.Authenticate(apiKey, Scope);
        if (app is null)
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "API key inválida ou sem permissão para esta operação.",
                Detail = $"Envie o header {HeaderName}. Esta rota exige a chave {(Scope == ApiKeyScope.Secret ? "secreta" : "pública ou secreta")} da app."
            })
            { StatusCode = StatusCodes.Status401Unauthorized };
            return;
        }

        context.HttpContext.Items[ItemKey] = app;
    }

    public static ClientApp GetClientApp(HttpContext httpContext)
        => httpContext.Items[ItemKey] as ClientApp
           ?? throw new InvalidOperationException($"Rota sem [{nameof(ApiKeyAuthAttribute)}].");
}

public static class ClientAppHttpContextExtensions
{
    public static ClientApp GetClientApp(this HttpContext httpContext) => ApiKeyAuthAttribute.GetClientApp(httpContext);
}
