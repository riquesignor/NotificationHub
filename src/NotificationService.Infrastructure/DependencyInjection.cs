using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NotificationService.Application.Services;
using NotificationService.Domain.Repositories;
using NotificationService.Infrastructure.Data;
using NotificationService.Infrastructure.Repositories;
using SendGrid;

namespace NotificationService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<ITemplateRepository, TemplateRepository>();

        services.AddScoped<INotificationService, NotificationOrchestrator>();
        services.AddScoped<IPushNotificationService, PushNotificationService>();
        services.AddScoped<IEmailNotificationService, EmailNotificationService>();

        if (FirebaseApp.DefaultInstance is null)
        {
            var credentialsPath = configuration["Firebase:CredentialsPath"] ?? "firebase-key.json";
            FirebaseApp.Create(new AppOptions
            {
                Credential = GoogleCredential.FromFile(credentialsPath)
            });
        }
        services.AddSingleton(_ => FirebaseMessaging.DefaultInstance);

        services.AddSingleton<ISendGridClient>(_ => new SendGridClient(configuration["SendGrid:ApiKey"]));

        services.AddHangfire(config => config
            .UsePostgreSqlStorage(configuration.GetConnectionString("DefaultConnection")));
        services.AddHangfireServer();

        return services;
    }
}
