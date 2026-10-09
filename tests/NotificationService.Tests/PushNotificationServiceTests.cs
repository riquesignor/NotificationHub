using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using NotificationService.Application.Services;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using Xunit;

namespace NotificationService.Tests;

public class PushNotificationServiceTests
{
    private sealed class FakeExpoHandler : HttpMessageHandler
    {
        private readonly string _responseJson;

        public FakeExpoHandler(string responseJson)
        {
            _responseJson = responseJson;
        }

        public List<string> RequestBodies { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseJson, Encoding.UTF8, "application/json")
            };
        }
    }

    private readonly InMemoryRepository<DeviceToken> _devices = new();

    private DeviceToken AddDevice(string appId, string userId, string token)
    {
        var device = new DeviceToken { Id = Guid.NewGuid(), AppId = appId, UserId = userId, Token = token, IsActive = true };
        _devices.Items.Add(device);
        return device;
    }

    private PushNotificationService Service(FakeExpoHandler handler)
        => new(new HttpClient(handler), _devices, Mock.Of<ILogger<PushNotificationService>>());

    private static Notification Notification(string appId = "app") => new()
    {
        Id = Guid.NewGuid(),
        AppId = appId,
        UserId = "user",
        Title = "Alerta",
        Message = "Granizo",
        Data = new() { ["route"] = "/alertas" },
        PushPriority = PushPriority.High,
        PushChannelId = "severe-alerts",
        PushTtlSeconds = 3600
    };

    [Fact]
    public async Task SendAsync_SendsOnlyThisAppsTokens_WithPushOptions()
    {
        AddDevice("app", "user", "ExponentPushToken[a]");
        AddDevice("other-app", "user", "ExponentPushToken[b]");
        var handler = new FakeExpoHandler("""{"data":[{"status":"ok","id":"ticket-1"}]}""");

        var result = await Service(handler).SendAsync(Notification());

        Assert.True(result.Success);
        Assert.Equal("ticket-1", result.MessageId);

        using var body = JsonDocument.Parse(Assert.Single(handler.RequestBodies));
        var message = Assert.Single(body.RootElement.EnumerateArray());
        Assert.Equal("ExponentPushToken[a]", message.GetProperty("to").GetString());
        Assert.Equal("high", message.GetProperty("priority").GetString());
        Assert.Equal("severe-alerts", message.GetProperty("channelId").GetString());
        Assert.Equal(3600, message.GetProperty("ttl").GetInt32());
        Assert.Equal("/alertas", message.GetProperty("data").GetProperty("route").GetString());
    }

    [Fact]
    public async Task SendAsync_OmitsPushOptions_WhenNotSet()
    {
        AddDevice("app", "user", "ExponentPushToken[a]");
        var handler = new FakeExpoHandler("""{"data":[{"status":"ok","id":"t"}]}""");
        var notification = Notification();
        notification.PushPriority = null;
        notification.PushChannelId = null;
        notification.PushTtlSeconds = null;

        await Service(handler).SendAsync(notification);

        using var body = JsonDocument.Parse(handler.RequestBodies[0]);
        var message = body.RootElement[0];
        Assert.False(message.TryGetProperty("priority", out _));
        Assert.False(message.TryGetProperty("channelId", out _));
        Assert.False(message.TryGetProperty("ttl", out _));
    }

    [Fact]
    public async Task SendAsync_DeactivatesTokensReportedAsNotRegistered()
    {
        var alive = AddDevice("app", "user", "ExponentPushToken[alive]");
        var dead = AddDevice("app", "user", "ExponentPushToken[dead]");
        var handler = new FakeExpoHandler("""
            {"data":[
              {"status":"ok","id":"t1"},
              {"status":"error","message":"not registered","details":{"error":"DeviceNotRegistered"}}
            ]}
            """);

        var result = await Service(handler).SendAsync(Notification());

        Assert.True(result.Success);
        Assert.True(alive.IsActive);
        Assert.False(dead.IsActive);
        Assert.Equal(1, _devices.SaveCount);
    }

    [Fact]
    public async Task SendAsync_Fails_WhenUserHasNoActiveDevice()
    {
        var handler = new FakeExpoHandler("""{"data":[]}""");

        var result = await Service(handler).SendAsync(Notification());

        Assert.False(result.Success);
        Assert.Empty(handler.RequestBodies);
    }
}
