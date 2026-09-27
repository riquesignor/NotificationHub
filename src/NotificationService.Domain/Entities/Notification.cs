using NotificationService.Domain.Enums;

namespace NotificationService.Domain.Entities;

public class Notification
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public Channel PrimaryChannel { get; set; }
    public Channel[] FallbackChannels { get; set; } = Array.Empty<Channel>();
    public Dictionary<string, string> Data { get; set; } = new();
    public NotificationStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public int RetryCount { get; set; }
    public string? ErrorMessage { get; set; }

    public User? User { get; set; }
    public ICollection<NotificationLog> Logs { get; set; } = new List<NotificationLog>();
}
