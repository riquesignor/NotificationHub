namespace NotificationService.Application.DTOs;

public record TemplateNotificationRequest
{
    public string UserId { get; init; } = string.Empty;
    public string TemplateName { get; init; } = string.Empty;
    public Dictionary<string, string> Variables { get; init; } = new();
    public string? IdempotencyKey { get; init; }
    public PushOptions? Push { get; init; }
}
