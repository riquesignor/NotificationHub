using NotificationService.Application.DTOs;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;

namespace NotificationService.Application.Services;

public interface INotificationService
{
    Task<NotificationResult> SendAsync(NotificationRequest request);
    Task<NotificationResult> SendFromTemplateAsync(TemplateNotificationRequest request);
    Task<IEnumerable<NotificationLog>> GetLogsAsync(Guid notificationId);
    Task<NotificationStatus?> GetStatusAsync(Guid notificationId);
    Task<bool> RetryAsync(Guid notificationId);
}
