using NotificationService.Domain.Enums;

namespace NotificationService.Domain.Entities;

public class Notification
{
    public Guid Id { get; set; }
    public string AppId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;

    // Chave opcional definida pela app: dois envios com a mesma chave (na
    // mesma app) resultam em uma única notificação.
    public string? IdempotencyKey { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public Channel PrimaryChannel { get; set; }
    public Channel[] FallbackChannels { get; set; } = Array.Empty<Channel>();
    public Dictionary<string, string> Data { get; set; } = new();

    // Opções específicas do canal Push — ignoradas pelos demais canais.
    public PushPriority? PushPriority { get; set; }
    public string? PushChannelId { get; set; }
    public int? PushTtlSeconds { get; set; }
    public NotificationStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public int RetryCount { get; set; }
    public string? ErrorMessage { get; set; }

    public User? User { get; set; }
    public ICollection<NotificationLog> Logs { get; set; } = new List<NotificationLog>();
}
