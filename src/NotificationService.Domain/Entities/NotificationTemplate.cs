using NotificationService.Domain.Enums;

namespace NotificationService.Domain.Entities;

public class NotificationTemplate
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TitleTemplate { get; set; } = string.Empty;
    public string MessageTemplate { get; set; } = string.Empty;
    public Channel[] Channels { get; set; } = Array.Empty<Channel>();
    public Dictionary<string, string>? DefaultData { get; set; }
    public DateTime CreatedAt { get; set; }
}
