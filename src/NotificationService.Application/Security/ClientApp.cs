namespace NotificationService.Application.Security;

public enum ApiKeyScope
{
    // Pode ir embutida no app cliente: só registra/remove o próprio aparelho.
    Public,

    // Só em servidor/job: envia notificações e lista aparelhos.
    Secret
}

public record ClientApp(string Id, string Name);

// Formato da seção "ClientApps" da configuração (appsettings / variáveis
// de ambiente, ex.: ClientApps__0__Id, ClientApps__0__SecretKey).
public record ClientAppDefinition
{
    public string Id { get; init; } = string.Empty;
    public string? Name { get; init; }
    public string PublicKey { get; init; } = string.Empty;
    public string SecretKey { get; init; } = string.Empty;
}
