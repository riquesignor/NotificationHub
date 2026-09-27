namespace NotificationService.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string? Email { get; set; }

    public ICollection<DeviceToken> DeviceTokens { get; set; } = new List<DeviceToken>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
