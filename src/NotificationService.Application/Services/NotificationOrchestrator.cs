using Microsoft.Extensions.Logging;
using NotificationService.Application.DTOs;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using NotificationService.Domain.Repositories;

namespace NotificationService.Application.Services;

public class NotificationOrchestrator : INotificationService
{
    public const int MaxRetries = 3;

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

    public async Task<NotificationResult> SendAsync(string appId, NotificationRequest request)
    {
        if (request.IdempotencyKey is not null)
        {
            var existing = await FindByIdempotencyKeyAsync(appId, request.IdempotencyKey);
            if (existing is not null)
            {
                _logger.LogInformation(
                    "Notification {NotificationId} reused for idempotency key {Key}",
                    existing.Id, request.IdempotencyKey);
                return ToResult(existing, duplicate: true);
            }
        }

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            AppId = appId,
            UserId = request.UserId,
            Title = request.Title,
            Message = request.Message,
            ImageUrl = request.ImageUrl,
            PrimaryChannel = request.PrimaryChannel,
            FallbackChannels = request.FallbackChannels,
            Data = request.Data,
            IdempotencyKey = request.IdempotencyKey,
            PushPriority = request.Push?.Priority,
            PushChannelId = request.Push?.ChannelId,
            PushTtlSeconds = request.Push?.TtlSeconds,
            Status = NotificationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        await _notificationRepo.AddAsync(notification);
        try
        {
            await _notificationRepo.SaveChangesAsync();
        }
        catch when (request.IdempotencyKey is not null)
        {
            // Duas requisições simultâneas com a mesma chave: o índice único
            // (AppId, IdempotencyKey) barra a segunda, que devolve a primeira.
            var winner = await FindByIdempotencyKeyAsync(appId, request.IdempotencyKey);
            if (winner is null)
            {
                throw;
            }
            return ToResult(winner, duplicate: true);
        }

        await DeliverAsync(notification);
        return ToResult(notification, duplicate: false);
    }

    public async Task<NotificationResult> SendFromTemplateAsync(string appId, TemplateNotificationRequest request)
    {
        var template = await _templateRepo.GetByNameAsync(appId, request.TemplateName)
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

        return await SendAsync(appId, new NotificationRequest
        {
            UserId = request.UserId,
            Title = title,
            Message = message,
            PrimaryChannel = primary,
            FallbackChannels = fallbacks,
            Data = data,
            IdempotencyKey = request.IdempotencyKey,
            Push = request.Push
        });
    }

    public async Task<IEnumerable<NotificationLog>> GetLogsAsync(Guid notificationId)
        => await _logRepo.GetAsync(l => l.NotificationId == notificationId);

    public async Task<NotificationStatus?> GetStatusAsync(string appId, Guid notificationId)
    {
        var notification = await _notificationRepo.GetByIdAsync(notificationId);
        return notification?.AppId == appId ? notification.Status : null;
    }

    public async Task<bool> RetryAsync(Guid notificationId)
    {
        var notification = await _notificationRepo.GetByIdAsync(notificationId);
        if (notification is null || notification.Status != NotificationStatus.Failed || notification.RetryCount >= MaxRetries)
        {
            return false;
        }

        // Reentrega a MESMA notificação (mesmo Id, logs e IdempotencyKey) em vez
        // de criar uma nova: antes a original ficava Failed para sempre e cada
        // retry gerava uma cópia.
        notification.RetryCount++;
        await DeliverAsync(notification);
        return true;
    }

    private async Task DeliverAsync(Notification notification)
    {
        var channels = new[] { notification.PrimaryChannel }
            .Concat(notification.FallbackChannels)
            .Distinct()
            .ToArray();

        var finalStatus = NotificationStatus.Failed;
        string? lastError = null;

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
                    finalStatus = NotificationStatus.Sent;
                    _logger.LogInformation(
                        "Notification {NotificationId} sent via {Channel}: {MessageId}",
                        notification.Id, channel, result.MessageId);
                    break;
                }

                lastError = result.ErrorMessage;
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                _logger.LogError(ex, "Failed to send via {Channel}", channel);
            }
        }

        await _logRepo.SaveChangesAsync();

        notification.Status = finalStatus;
        notification.ErrorMessage = finalStatus == NotificationStatus.Sent ? null : lastError;
        if (finalStatus == NotificationStatus.Sent)
        {
            notification.SentAt = DateTime.UtcNow;
        }
        await _notificationRepo.UpdateAsync(notification);
        await _notificationRepo.SaveChangesAsync();
    }

    private async Task<Notification?> FindByIdempotencyKeyAsync(string appId, string key)
        => (await _notificationRepo.GetAsync(n => n.AppId == appId && n.IdempotencyKey == key)).FirstOrDefault();

    private static NotificationResult ToResult(Notification notification, bool duplicate) => new()
    {
        NotificationId = notification.Id,
        Status = notification.Status,
        Success = notification.Status == NotificationStatus.Sent,
        Duplicate = duplicate
    };

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
