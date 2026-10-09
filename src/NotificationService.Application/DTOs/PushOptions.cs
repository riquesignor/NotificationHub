using NotificationService.Domain.Enums;

namespace NotificationService.Application.DTOs;

// Opções que só fazem sentido no canal Push (repassadas à Expo Push API).
public record PushOptions
{
    public PushPriority? Priority { get; init; }

    // Canal de notificação do Android (precisa ser criado pela app).
    public string? ChannelId { get; init; }

    // Por quanto tempo a Expo/FCM/APNs tenta entregar se o aparelho estiver offline.
    public int? TtlSeconds { get; init; }
}
