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

    // Repetir o envio com a mesma chave não gera nova notificação — devolve a
    // já existente (útil para jobs que reavaliam a mesma condição várias vezes).
    public string? IdempotencyKey { get; init; }
    public PushOptions? Push { get; init; }
}
