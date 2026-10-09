using NotificationService.Domain.Enums;

namespace NotificationService.Domain.Entities;

public class DeviceToken
{
    public Guid Id { get; set; }

    // App consumidora dona deste token (ver ClientApp na API). Tokens de apps
    // diferentes nunca se misturam, mesmo que o UserId coincida.
    public string AppId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DeviceType DeviceType { get; set; }
    public string? DeviceName { get; set; }
    public bool IsActive { get; set; } = true;

    // Metadados livres definidos pela app (ex.: idioma, plano, região,
    // preferências). O hub não interpreta — só guarda e permite filtrar.
    public Dictionary<string, string> Attributes { get; set; } = new();
    public DateTime RegisteredAt { get; set; }
    public DateTime? LastUsedAt { get; set; }

    public User? User { get; set; }
}
