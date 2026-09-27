using Microsoft.Extensions.Logging;
using NotificationService.Application.Services;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using NotificationService.Domain.Repositories;

namespace NotificationService.Application.Jobs;

public class RetryFailedNotificationsJob
{
    private readonly INotificationService _notificationService;
    private readonly IRepository<Notification> _notificationRepo;
    private readonly ILogger<RetryFailedNotificationsJob> _logger;

    public RetryFailedNotificationsJob(
        INotificationService notificationService,
        IRepository<Notification> notificationRepo,
        ILogger<RetryFailedNotificationsJob> logger)
    {
        _notificationService = notificationService;
        _notificationRepo = notificationRepo;
        _logger = logger;
    }

    public async Task Execute()
    {
        _logger.LogInformation("Starting retry job for failed notifications");

        var failed = (await _notificationRepo.GetAsync(
            n => n.Status == NotificationStatus.Failed && n.RetryCount < 3)).ToList();

        foreach (var notification in failed)
        {
            await _notificationService.RetryAsync(notification.Id);
        }

        _logger.LogInformation("Retry job completed. {Count} retried", failed.Count);
    }
}
