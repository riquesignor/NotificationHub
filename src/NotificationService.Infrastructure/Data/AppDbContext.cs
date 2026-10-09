using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;

namespace NotificationService.Infrastructure.Data;

public class AppDbContext : DbContext
{
    private const int AppIdMaxLength = 100;

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // User.Id é uma string externa (ex.: UID do Firebase Auth) sem
        // garantia de existir localmente — sem FK real com Notification/DeviceToken.
        modelBuilder.Entity<Notification>().Ignore(n => n.User);
        modelBuilder.Entity<DeviceToken>().Ignore(d => d.User);
        modelBuilder.Entity<User>().Ignore(u => u.Notifications);
        modelBuilder.Entity<User>().Ignore(u => u.DeviceTokens);

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.Property(n => n.AppId).HasMaxLength(AppIdMaxLength);
            entity.Property(n => n.IdempotencyKey).HasMaxLength(200);
            entity.Property(n => n.PrimaryChannel).HasConversion<string>();
            entity.Property(n => n.PushPriority).HasConversion<string>();
            entity.Property(n => n.FallbackChannels).HasConversion(
                v => string.Join(',', v.Select(c => c.ToString())),
                v => v.Length == 0
                    ? Array.Empty<Channel>()
                    : v.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Enum.Parse<Channel>).ToArray());
            MapJsonDictionary(entity.Property(n => n.Data));
            entity.HasMany(n => n.Logs).WithOne(l => l.Notification).HasForeignKey(l => l.NotificationId);

            // Garante a idempotência mesmo com requisições simultâneas
            // (no Postgres, várias linhas com chave NULL não conflitam).
            entity.HasIndex(n => new { n.AppId, n.IdempotencyKey }).IsUnique();
            entity.HasIndex(n => new { n.Status, n.RetryCount });
        });

        modelBuilder.Entity<NotificationLog>(entity =>
        {
            entity.Property(l => l.Channel).HasConversion<string>();
            entity.Property(l => l.Status).HasConversion<string>();
        });

        modelBuilder.Entity<NotificationTemplate>(entity =>
        {
            entity.Property(t => t.AppId).HasMaxLength(AppIdMaxLength);
            entity.Property(t => t.Channels).HasConversion(
                v => string.Join(',', v.Select(c => c.ToString())),
                v => v.Length == 0
                    ? Array.Empty<Channel>()
                    : v.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Enum.Parse<Channel>).ToArray());
            entity.Property(t => t.DefaultData).HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => v == null ? null : JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null),
                new ValueComparer<Dictionary<string, string>?>(
                    (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(),
                    v => v == null ? null : new Dictionary<string, string>(v)));
            entity.HasIndex(t => new { t.AppId, t.Name }).IsUnique();
        });

        modelBuilder.Entity<DeviceToken>(entity =>
        {
            entity.Property(d => d.AppId).HasMaxLength(AppIdMaxLength);
            entity.Property(d => d.DeviceType).HasConversion<string>();
            MapJsonDictionary(entity.Property(d => d.Attributes));

            // Um token de push pertence a um único registro por app.
            entity.HasIndex(d => new { d.AppId, d.Token }).IsUnique();
            entity.HasIndex(d => new { d.AppId, d.UserId, d.IsActive });
        });
    }

    // Dicionário guardado como JSON em coluna texto. O ValueComparer faz o EF
    // perceber mudanças DENTRO do dicionário (sem ele, só troca de instância).
    private static void MapJsonDictionary(PropertyBuilder<Dictionary<string, string>> property)
    {
        property.HasConversion(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) ?? new(),
            new ValueComparer<Dictionary<string, string>>(
                (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(),
                v => new Dictionary<string, string>(v)));
    }
}
