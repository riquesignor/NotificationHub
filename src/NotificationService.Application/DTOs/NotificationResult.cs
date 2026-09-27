using NotificationService.Domain.Enums;

namespace NotificationService.Application.DTOs;

public record NotificationResult
{
    public Guid NotificationId { get; init; }
    public NotificationStatus Status { get; init; }
    public bool Success { get; init; }
}
