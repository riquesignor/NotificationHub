using NotificationService.Domain.Enums;

namespace NotificationService.Application.DTOs;

public record NotificationRequest
{
    public string UserId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? ImageUrl { get; init; }
    public Channel PrimaryChannel { get; init; } = Channel.Push;
    public Channel[] FallbackChannels { get; init; } = Array.Empty<Channel>();
    public Dictionary<string, string> Data { get; init; } = new();
}
