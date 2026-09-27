namespace NotificationService.Application.DTOs;

public record SendResult
{
    public bool Success { get; init; }
    public string? MessageId { get; init; }
    public string? ErrorMessage { get; init; }
}
