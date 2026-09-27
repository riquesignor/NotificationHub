using NotificationService.Application.DTOs;
using NotificationService.Domain.Entities;

namespace NotificationService.Application.Services;

public interface IPushNotificationService
{
    Task<SendResult> SendAsync(Notification notification);
}
