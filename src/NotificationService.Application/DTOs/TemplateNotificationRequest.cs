namespace NotificationService.Application.DTOs;

public record TemplateNotificationRequest
{
    public Guid UserId { get; init; }
    public string TemplateName { get; init; } = string.Empty;
    public Dictionary<string, string> Variables { get; init; } = new();
}
