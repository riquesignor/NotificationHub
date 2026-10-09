using NotificationService.Domain.Enums;

namespace NotificationService.Application.DTOs;

public record RegisterDeviceRequest
{
    public string UserId { get; init; } = string.Empty;
    public string Token { get; init; } = string.Empty;
    public DeviceType DeviceType { get; init; }
    public string? DeviceName { get; init; }

    // Substitui os atributos anteriores do aparelho (não faz merge).
    public Dictionary<string, string>? Attributes { get; init; }
}

public record UnregisterDeviceRequest
{
    public string Token { get; init; } = string.Empty;
}

public record DeviceQuery
{
    public string? UserId { get; init; }

    // Igualdade exata por atributo; todos precisam bater.
    public Dictionary<string, string> Attributes { get; init; } = new();
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 100;
}

// O token de push não é exposto na listagem — quem consome a lista só
// precisa do UserId para chamar /send.
public record DeviceSummary(
    string UserId,
    DeviceType DeviceType,
    string? DeviceName,
    Dictionary<string, string> Attributes,
    DateTime RegisteredAt,
    DateTime? LastUsedAt);

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);
