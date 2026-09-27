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
    private readonly Mock<IPushNotificationService> _pushService = new();
    private readonly Mock<IEmailNotificationService> _emailService = new();
    private readonly Mock<IRepository<Notification>> _notificationRepo = new();
    private readonly Mock<IRepository<NotificationLog>> _logRepo = new();
    private readonly Mock<ITemplateRepository> _templateRepo = new();
    private readonly NotificationOrchestrator _sut;

    public NotificationOrchestratorTests()
    {
        _sut = new NotificationOrchestrator(
            _pushService.Object,
            _emailService.Object,
            _notificationRepo.Object,
            _logRepo.Object,
            _templateRepo.Object,
            Mock.Of<ILogger<NotificationOrchestrator>>());
    }

    [Fact]
    public async Task SendAsync_ReturnsSuccess_WhenPrimaryChannelSucceeds()
    {
        _pushService.Setup(s => s.SendAsync(It.IsAny<Notification>()))
            .ReturnsAsync(new SendResult { Success = true, MessageId = "msg-1" });

        var request = new NotificationRequest
        {
            UserId = Guid.NewGuid(),
            Title = "Title",
            Message = "Message",
            PrimaryChannel = Channel.Push
        };

        var result = await _sut.SendAsync(request);

        Assert.True(result.Success);
        Assert.Equal(NotificationStatus.Sent, result.Status);
        _emailService.Verify(s => s.SendAsync(It.IsAny<Notification>()), Times.Never);
    }

    [Fact]
    public async Task SendAsync_FallsBackToEmail_WhenPushFails()
    {
        _pushService.Setup(s => s.SendAsync(It.IsAny<Notification>()))
            .ReturnsAsync(new SendResult { Success = false, ErrorMessage = "no token" });
        _emailService.Setup(s => s.SendAsync(It.IsAny<Notification>()))
            .ReturnsAsync(new SendResult { Success = true, MessageId = "msg-2" });

        var request = new NotificationRequest
        {
            UserId = Guid.NewGuid(),
            Title = "Title",
            Message = "Message",
            PrimaryChannel = Channel.Push,
            FallbackChannels = new[] { Channel.Email }
        };

        var result = await _sut.SendAsync(request);

        Assert.True(result.Success);
        _emailService.Verify(s => s.SendAsync(It.IsAny<Notification>()), Times.Once);
    }

    [Fact]
    public async Task RetryAsync_ReturnsFalse_WhenRetryLimitReached()
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            Status = NotificationStatus.Failed,
            RetryCount = 3
        };

        _notificationRepo.Setup(r => r.GetByIdAsync(notification.Id)).ReturnsAsync(notification);

        var result = await _sut.RetryAsync(notification.Id);

        Assert.False(result);
    }
}
