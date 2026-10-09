using System.Security.Cryptography;
using System.Text;

namespace NotificationService.Application.Security;

public interface IClientAppRegistry
{
    // Sem nenhuma app cadastrada o hub roda em modo legado (aberto), para não
    // quebrar consumidoras que ainda não enviam chave.
    bool IsLegacyMode { get; }

    /// <returns>A app dona da chave, se ela der acesso ao escopo pedido.</returns>
    ClientApp? Authenticate(string? apiKey, ApiKeyScope requiredScope);
}

public class ClientAppRegistry : IClientAppRegistry
{
    public const string LegacyAppId = "default";

    private readonly List<(ClientApp App, byte[] PublicKey, byte[] SecretKey)> _apps;

    public ClientAppRegistry(IEnumerable<ClientAppDefinition> definitions)
    {
        _apps = definitions
            .Where(d => !string.IsNullOrWhiteSpace(d.Id))
            .Select(d =>
            {
                if (string.IsNullOrWhiteSpace(d.PublicKey) || string.IsNullOrWhiteSpace(d.SecretKey))
                {
                    throw new InvalidOperationException($"ClientApp '{d.Id}' precisa de PublicKey e SecretKey.");
                }
                if (d.PublicKey == d.SecretKey)
                {
                    throw new InvalidOperationException($"ClientApp '{d.Id}': PublicKey e SecretKey devem ser diferentes.");
                }
                return (new ClientApp(d.Id, d.Name ?? d.Id), Hash(d.PublicKey), Hash(d.SecretKey));
            })
            .ToList();

        var duplicated = _apps.GroupBy(a => a.App.Id).FirstOrDefault(g => g.Count() > 1);
        if (duplicated is not null)
        {
            throw new InvalidOperationException($"ClientApp '{duplicated.Key}' cadastrada mais de uma vez.");
        }
    }

    public bool IsLegacyMode => _apps.Count == 0;

    public ClientApp? Authenticate(string? apiKey, ApiKeyScope requiredScope)
    {
        if (IsLegacyMode)
        {
            return new ClientApp(LegacyAppId, "Legacy (sem chaves configuradas)");
        }

        if (string.IsNullOrEmpty(apiKey))
        {
            return null;
        }

        var hash = Hash(apiKey);
        foreach (var (app, publicKey, secretKey) in _apps)
        {
            // Comparação em tempo constante (sobre o hash, que tem tamanho fixo).
            if (CryptographicOperations.FixedTimeEquals(hash, secretKey))
            {
                return app;
            }
            if (requiredScope == ApiKeyScope.Public && CryptographicOperations.FixedTimeEquals(hash, publicKey))
            {
                return app;
            }
        }

        return null;
    }

    private static byte[] Hash(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));
}
