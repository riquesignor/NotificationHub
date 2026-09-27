using NotificationService.Domain.Enums;

namespace NotificationService.Domain.Entities;

public class DeviceToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DeviceType DeviceType { get; set; }
    public string? DeviceName { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime RegisteredAt { get; set; }
    public DateTime? LastUsedAt { get; set; }

    public User? User { get; set; }
}
