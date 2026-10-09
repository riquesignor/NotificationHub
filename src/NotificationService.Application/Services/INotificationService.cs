using NotificationService.Application.DTOs;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;

namespace NotificationService.Application.Services;

public interface INotificationService
{
    Task<NotificationResult> SendAsync(string appId, NotificationRequest request);
    Task<NotificationResult> SendFromTemplateAsync(string appId, TemplateNotificationRequest request);
    Task<IEnumerable<NotificationLog>> GetLogsAsync(Guid notificationId);

    /// <returns>null se a notificação não existe ou pertence a outra app.</returns>
    Task<NotificationStatus?> GetStatusAsync(string appId, Guid notificationId);
    Task<bool> RetryAsync(Guid notificationId);
}
