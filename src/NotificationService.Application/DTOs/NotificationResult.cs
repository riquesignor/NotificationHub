using NotificationService.Domain.Enums;

namespace NotificationService.Application.DTOs;

public record NotificationResult
{
    public Guid NotificationId { get; init; }
    public NotificationStatus Status { get; init; }
    public bool Success { get; init; }

    // true quando a IdempotencyKey já tinha sido usada: nada foi reenviado.
    public bool Duplicate { get; init; }
}
