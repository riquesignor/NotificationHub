namespace NotificationService.Domain.Entities;

public class User
{
    // Id é o identificador externo do usuário na app consumidora
    // (ex.: UID do Firebase Auth) — o NotificationHub não gera esse id.
    public string Id { get; set; } = string.Empty;
    public string? Email { get; set; }

    public ICollection<DeviceToken> DeviceTokens { get; set; } = new List<DeviceToken>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
