using FirebaseAdmin.Messaging;
using Microsoft.Extensions.Logging;
using NotificationService.Application.DTOs;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Repositories;

namespace NotificationService.Application.Services;

public class PushNotificationService : IPushNotificationService
{
    private readonly FirebaseMessaging _fcm;
    private readonly IRepository<DeviceToken> _deviceTokenRepo;
    private readonly ILogger<PushNotificationService> _logger;

    public PushNotificationService(
        FirebaseMessaging fcm,
        IRepository<DeviceToken> deviceTokenRepo,
        ILogger<PushNotificationService> logger)
    {
        _fcm = fcm;
        _deviceTokenRepo = deviceTokenRepo;
        _logger = logger;
    }

    public async Task<SendResult> SendAsync(Notification notification)
    {
        try
        {
            var tokens = await _deviceTokenRepo.GetAsync(
                dt => dt.UserId == notification.UserId && dt.IsActive);

            var tokenList = tokens.Select(t => t.Token).ToList();
            if (tokenList.Count == 0)
            {
                return new SendResult { Success = false, ErrorMessage = "No active device tokens" };
            }

            var message = new MulticastMessage
            {
                Notification = new FirebaseAdmin.Messaging.Notification
                {
                    Title = notification.Title,
                    Body = notification.Message,
                    ImageUrl = notification.ImageUrl
                },
                Data = notification.Data,
                Tokens = tokenList
            };

            var response = await _fcm.SendEachForMulticastAsync(message);

            _logger.LogInformation("Push sent to {SuccessCount} devices", response.SuccessCount);

            return new SendResult
            {
                Success = response.SuccessCount > 0,
                MessageId = response.SuccessCount.ToString(),
                ErrorMessage = response.FailureCount > 0
                    ? $"Failed to {response.FailureCount} devices"
                    : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Push notification failed");
            return new SendResult { Success = false, ErrorMessage = ex.Message };
        }
    }
}
