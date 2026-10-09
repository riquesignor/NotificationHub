using Microsoft.Extensions.Logging;
using Moq;
using NotificationService.Application.DTOs;
using NotificationService.Application.Services;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using NotificationService.Domain.Repositories;
using Xunit;

namespace NotificationService.Tests;

public class NotificationOrchestratorTests
{
    private const string AppId = "app-a";

    private readonly Mock<IPushNotificationService> _pushService = new();
    private readonly Mock<IEmailNotificationService> _emailService = new();
    private readonly InMemoryRepository<Notification> _notificationRepo = new();
    private readonly InMemoryRepository<NotificationLog> _logRepo = new();
    private readonly Mock<ITemplateRepository> _templateRepo = new();
    private readonly NotificationOrchestrator _sut;

    public NotificationOrchestratorTests()
    {
        _sut = new NotificationOrchestrator(
            _pushService.Object,
            _emailService.Object,
            _notificationRepo,
            _logRepo,
            _templateRepo.Object,
            Mock.Of<ILogger<NotificationOrchestrator>>());
    }

    private static NotificationRequest Request(string userId = "user-1", string? idempotencyKey = null) => new()
    {
        UserId = userId,
        Title = "Title",
        Message = "Message",
        PrimaryChannel = Channel.Push,
        IdempotencyKey = idempotencyKey
    };

    private void PushReturns(bool success) =>
        _pushService.Setup(s => s.SendAsync(It.IsAny<Notification>()))
            .ReturnsAsync(new SendResult { Success = success, MessageId = success ? "msg" : null, ErrorMessage = success ? null : "fail" });

    [Fact]
    public async Task SendAsync_ReturnsSuccess_WhenPrimaryChannelSucceeds()
    {
        PushReturns(true);

        var result = await _sut.SendAsync(AppId, Request());

        Assert.True(result.Success);
        Assert.False(result.Duplicate);
        Assert.Equal(NotificationStatus.Sent, result.Status);
        _emailService.Verify(s => s.SendAsync(It.IsAny<Notification>()), Times.Never);
    }

    [Fact]
    public async Task SendAsync_FallsBackToEmail_WhenPushFails()
    {
        PushReturns(false);
        _emailService.Setup(s => s.SendAsync(It.IsAny<Notification>()))
            .ReturnsAsync(new SendResult { Success = true, MessageId = "msg-2" });

        var result = await _sut.SendAsync(AppId, Request() with { FallbackChannels = new[] { Channel.Email } });

        Assert.True(result.Success);
        _emailService.Verify(s => s.SendAsync(It.IsAny<Notification>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_StoresAppIdAndPushOptions()
    {
        PushReturns(true);

        await _sut.SendAsync(AppId, Request() with
        {
            Push = new PushOptions { Priority = PushPriority.High, ChannelId = "alerts", TtlSeconds = 600 }
        });

        var stored = Assert.Single(_notificationRepo.Items);
        Assert.Equal(AppId, stored.AppId);
        Assert.Equal(PushPriority.High, stored.PushPriority);
        Assert.Equal("alerts", stored.PushChannelId);
        Assert.Equal(600, stored.PushTtlSeconds);
    }

    [Fact]
    public async Task SendAsync_WithRepeatedIdempotencyKey_DeliversOnlyOnce()
    {
        PushReturns(true);

        var first = await _sut.SendAsync(AppId, Request(idempotencyKey: "alert-42"));
        var second = await _sut.SendAsync(AppId, Request(idempotencyKey: "alert-42"));

        Assert.False(first.Duplicate);
        Assert.True(second.Duplicate);
        Assert.Equal(first.NotificationId, second.NotificationId);
        Assert.Single(_notificationRepo.Items);
        _pushService.Verify(s => s.SendAsync(It.IsAny<Notification>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_SameIdempotencyKeyInDifferentApps_AreIndependent()
    {
        PushReturns(true);

        var a = await _sut.SendAsync("app-a", Request(idempotencyKey: "k"));
        var b = await _sut.SendAsync("app-b", Request(idempotencyKey: "k"));

        Assert.False(b.Duplicate);
        Assert.NotEqual(a.NotificationId, b.NotificationId);
        Assert.Equal(2, _notificationRepo.Items.Count);
    }

    [Fact]
    public async Task RetryAsync_RedeliversSameNotification_InsteadOfCreatingCopy()
    {
        PushReturns(false);
        var failed = await _sut.SendAsync(AppId, Request(idempotencyKey: "k"));
        Assert.Equal(NotificationStatus.Failed, failed.Status);

        PushReturns(true);
        var retried = await _sut.RetryAsync(failed.NotificationId);

        Assert.True(retried);
        var notification = Assert.Single(_notificationRepo.Items);
        Assert.Equal(failed.NotificationId, notification.Id);
        Assert.Equal(NotificationStatus.Sent, notification.Status);
        Assert.Equal(1, notification.RetryCount);
        Assert.Null(notification.ErrorMessage);
        Assert.Equal(new[] { 1, 2 }, _logRepo.Items.Select(l => l.AttemptNumber).OrderBy(n => n));
    }

    [Fact]
    public async Task RetryAsync_ReturnsFalse_WhenRetryLimitReached()
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            Status = NotificationStatus.Failed,
            RetryCount = NotificationOrchestrator.MaxRetries
        };
        _notificationRepo.Items.Add(notification);

        var result = await _sut.RetryAsync(notification.Id);

        Assert.False(result);
        _pushService.Verify(s => s.SendAsync(It.IsAny<Notification>()), Times.Never);
    }

    [Fact]
    public async Task GetStatusAsync_HidesNotificationsFromOtherApps()
    {
        PushReturns(true);
        var result = await _sut.SendAsync("app-a", Request());

        Assert.Equal(NotificationStatus.Sent, await _sut.GetStatusAsync("app-a", result.NotificationId));
        Assert.Null(await _sut.GetStatusAsync("app-b", result.NotificationId));
    }
}
