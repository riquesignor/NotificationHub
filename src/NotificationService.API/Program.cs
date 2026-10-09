using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using Hangfire;
using Microsoft.OpenApi.Models;
using NotificationService.API.Security;
using NotificationService.Application.Jobs;
using NotificationService.Application.Security;
using NotificationService.Application.Validators;
using NotificationService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddValidatorsFromAssemblyContaining<NotificationRequestValidator>();

// Apps consumidoras e suas chaves (seção "ClientApps" — em produção, via
// variáveis de ambiente ClientApps__0__Id, ClientApps__0__PublicKey, ...).
var clientApps = builder.Configuration.GetSection("ClientApps").Get<List<ClientAppDefinition>>() ?? new();
builder.Services.AddSingleton<IClientAppRegistry>(new ClientAppRegistry(clientApps));

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var scheme = new OpenApiSecurityScheme
    {
        Name = ApiKeyAuthAttribute.HeaderName,
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Description = "Chave pública (registro de aparelho) ou secreta (todo o resto) da app.",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ApiKey" }
    };
    options.AddSecurityDefinition("ApiKey", scheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = Array.Empty<string>() });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

if (app.Services.GetRequiredService<IClientAppRegistry>().IsLegacyMode)
{
    app.Logger.LogWarning(
        "Nenhuma ClientApp configurada: API rodando SEM autenticação (modo legado, AppId '{AppId}'). " +
        "Configure a seção ClientApps antes de expor o hub publicamente.",
        ClientAppRegistry.LegacyAppId);
}

// API baseada em serviço (IRecurringJobManager) em vez da estática
// RecurringJob.AddOrUpdate — a estática depende de JobStorage.Current
// já resolvido no momento da chamada, e essa ordem não é garantida.
using (var scope = app.Services.CreateScope())
{
    var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
    recurringJobManager.AddOrUpdate<RetryFailedNotificationsJob>(
        "retry-failed-notifications",
        job => job.Execute(),
        Cron.Hourly);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHangfireDashboard();
app.UseCors("AllowAll");
app.MapControllers();

app.Run();
