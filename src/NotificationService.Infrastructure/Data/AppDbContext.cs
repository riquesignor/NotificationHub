using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;

namespace NotificationService.Infrastructure.Data;

public class AppDbContext : DbContext
{
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
            entity.Property(n => n.PrimaryChannel).HasConversion<string>();
            entity.Property(n => n.FallbackChannels).HasConversion(
                v => string.Join(',', v.Select(c => c.ToString())),
                v => v.Length == 0
                    ? Array.Empty<Channel>()
                    : v.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Enum.Parse<Channel>).ToArray());
            entity.Property(n => n.Data).HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) ?? new());
            entity.HasMany(n => n.Logs).WithOne(l => l.Notification).HasForeignKey(l => l.NotificationId);
        });

        modelBuilder.Entity<NotificationLog>(entity =>
        {
            entity.Property(l => l.Channel).HasConversion<string>();
            entity.Property(l => l.Status).HasConversion<string>();
        });

        modelBuilder.Entity<NotificationTemplate>(entity =>
        {
            entity.Property(t => t.Channels).HasConversion(
                v => string.Join(',', v.Select(c => c.ToString())),
                v => v.Length == 0
                    ? Array.Empty<Channel>()
                    : v.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Enum.Parse<Channel>).ToArray());
            entity.Property(t => t.DefaultData).HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => v == null ? null : JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null));
        });

        modelBuilder.Entity<DeviceToken>(entity =>
        {
            entity.Property(d => d.DeviceType).HasConversion<string>();
        });
    }
}
