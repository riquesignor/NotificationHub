using Hangfire;
using Hangfire.PostgreSql;
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
        services.AddScoped<IUserRepository, UserRepository>();

        services.AddScoped<INotificationService, NotificationOrchestrator>();
        services.AddHttpClient<IPushNotificationService, PushNotificationService>();
        services.AddScoped<IEmailNotificationService, EmailNotificationService>();

        services.AddSingleton<ISendGridClient>(_ => new SendGridClient(configuration["SendGrid:ApiKey"]));

        services.AddHangfire(config => config
            .UsePostgreSqlStorage(options =>
                options.UseNpgsqlConnection(configuration.GetConnectionString("DefaultConnection"))));
        services.AddHangfireServer();

        return services;
    }
}
