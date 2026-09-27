using Microsoft.Extensions.Logging;
using NotificationService.Application.DTOs;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using NotificationService.Domain.Repositories;

namespace NotificationService.Application.Services;

public class NotificationOrchestrator : INotificationService
{
    private readonly IPushNotificationService _pushService;
    private readonly IEmailNotificationService _emailService;
    private readonly IRepository<Notification> _notificationRepo;
    private readonly IRepository<NotificationLog> _logRepo;
    private readonly ITemplateRepository _templateRepo;
    private readonly ILogger<NotificationOrchestrator> _logger;

    public NotificationOrchestrator(
        IPushNotificationService pushService,
        IEmailNotificationService emailService,
        IRepository<Notification> notificationRepo,
        IRepository<NotificationLog> logRepo,
        ITemplateRepository templateRepo,
        ILogger<NotificationOrchestrator> logger)
    {
        _pushService = pushService;
        _emailService = emailService;
        _notificationRepo = notificationRepo;
        _logRepo = logRepo;
        _templateRepo = templateRepo;
        _logger = logger;
    }

    public async Task<NotificationResult> SendAsync(NotificationRequest request)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            Title = request.Title,
            Message = request.Message,
            ImageUrl = request.ImageUrl,
            PrimaryChannel = request.PrimaryChannel,
            FallbackChannels = request.FallbackChannels,
            Data = request.Data,
            Status = NotificationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        await _notificationRepo.AddAsync(notification);
        await _notificationRepo.SaveChangesAsync();

        var channels = new[] { request.PrimaryChannel }
            .Concat(request.FallbackChannels)
            .Distinct()
            .ToArray();

        var lastStatus = NotificationStatus.Failed;

        foreach (var channel in channels)
        {
            try
            {
                var result = channel switch
                {
                    Channel.Push => await _pushService.SendAsync(notification),
                    Channel.Email => await _emailService.SendAsync(notification),
                    _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, "Unknown channel")
                };

                await _logRepo.AddAsync(new NotificationLog
                {
                    Id = Guid.NewGuid(),
                    NotificationId = notification.Id,
                    Channel = channel,
                    Status = result.Success ? NotificationStatus.Sent : NotificationStatus.Failed,
                    MessageId = result.MessageId,
                    ErrorMessage = result.ErrorMessage,
                    AttemptedAt = DateTime.UtcNow,
                    AttemptNumber = notification.RetryCount + 1
                });

                if (result.Success)
                {
                    lastStatus = NotificationStatus.Sent;
                    _logger.LogInformation(
                        "Notification {NotificationId} sent via {Channel}: {MessageId}",
                        notification.Id, channel, result.MessageId);
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send via {Channel}", channel);
            }
        }

        await _logRepo.SaveChangesAsync();

        notification.Status = lastStatus;
        notification.SentAt = DateTime.UtcNow;
        await _notificationRepo.UpdateAsync(notification);
        await _notificationRepo.SaveChangesAsync();

        return new NotificationResult
        {
            NotificationId = notification.Id,
            Status = lastStatus,
            Success = lastStatus == NotificationStatus.Sent
        };
    }

    public async Task<NotificationResult> SendFromTemplateAsync(TemplateNotificationRequest request)
    {
        var template = await _templateRepo.GetByNameAsync(request.TemplateName)
            ?? throw new InvalidOperationException($"Template '{request.TemplateName}' not found");

        var title = ApplyVariables(template.TitleTemplate, request.Variables);
        var message = ApplyVariables(template.MessageTemplate, request.Variables);

        var primary = template.Channels.FirstOrDefault();
        var fallbacks = template.Channels.Skip(1).ToArray();

        var data = new Dictionary<string, string>(template.DefaultData ?? new Dictionary<string, string>());
        foreach (var (key, value) in request.Variables)
        {
            data[key] = value;
        }

        return await SendAsync(new NotificationRequest
        {
            UserId = request.UserId,
            Title = title,
            Message = message,
            PrimaryChannel = primary,
            FallbackChannels = fallbacks,
            Data = data
        });
    }

    public async Task<IEnumerable<NotificationLog>> GetLogsAsync(Guid notificationId)
        => await _logRepo.GetAsync(l => l.NotificationId == notificationId);

    public async Task<NotificationStatus?> GetStatusAsync(Guid notificationId)
    {
        var notification = await _notificationRepo.GetByIdAsync(notificationId);
        return notification?.Status;
    }

    public async Task<bool> RetryAsync(Guid notificationId)
    {
        var notification = await _notificationRepo.GetByIdAsync(notificationId);
        if (notification is null || notification.Status != NotificationStatus.Failed || notification.RetryCount >= 3)
        {
            return false;
        }

        notification.RetryCount++;
        await _notificationRepo.UpdateAsync(notification);
        await _notificationRepo.SaveChangesAsync();

        await SendAsync(new NotificationRequest
        {
            UserId = notification.UserId,
            Title = notification.Title,
            Message = notification.Message,
            ImageUrl = notification.ImageUrl,
            PrimaryChannel = notification.PrimaryChannel,
            FallbackChannels = notification.FallbackChannels,
            Data = notification.Data
        });

        return true;
    }

    private static string ApplyVariables(string template, Dictionary<string, string> variables)
    {
        var result = template;
        foreach (var (key, value) in variables)
        {
            result = result.Replace($"{{{key}}}", value);
        }
        return result;
    }
}
