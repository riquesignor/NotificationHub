using NotificationService.Domain.Enums;

namespace NotificationService.Domain.Entities;

public class NotificationLog
{
    public Guid Id { get; set; }
    public Guid NotificationId { get; set; }
    public Channel Channel { get; set; }
    public NotificationStatus Status { get; set; }
    public string? MessageId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime AttemptedAt { get; set; }
    public int AttemptNumber { get; set; }

    public Notification? Notification { get; set; }
}
