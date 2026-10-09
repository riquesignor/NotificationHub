using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using NotificationService.Application.DTOs;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using NotificationService.Domain.Repositories;

namespace NotificationService.Application.Services;

// Envia via Expo Push API em vez de Firebase Admin SDK direto: os apps
// consumidores (Expo/React Native) coletam ExponentPushToken[...], não
// token nativo de FCM, e a Expo Push API não pede nenhuma credencial.
public class PushNotificationService : IPushNotificationService
{
    public const string ExpoPushEndpoint = "https://exp.host/--/api/v2/push/send";

    // Limite de mensagens por requisição da Expo Push API.
    private const int MaxMessagesPerRequest = 100;

    private readonly HttpClient _httpClient;
    private readonly IRepository<DeviceToken> _deviceTokenRepo;
    private readonly ILogger<PushNotificationService> _logger;

    public PushNotificationService(
        HttpClient httpClient,
        IRepository<DeviceToken> deviceTokenRepo,
        ILogger<PushNotificationService> logger)
    {
        _httpClient = httpClient;
        _deviceTokenRepo = deviceTokenRepo;
        _logger = logger;
    }

    public async Task<SendResult> SendAsync(Notification notification)
    {
        try
        {
            var devices = (await _deviceTokenRepo.GetAsync(dt =>
                dt.AppId == notification.AppId && dt.UserId == notification.UserId && dt.IsActive)).ToList();

            if (devices.Count == 0)
            {
                return new SendResult { Success = false, ErrorMessage = "No active device tokens" };
            }

            var successCount = 0;
            string? firstMessageId = null;
            var errors = new List<string>();
            var deadDevices = new List<DeviceToken>();

            foreach (var chunk in devices.Chunk(MaxMessagesPerRequest))
            {
                var messages = chunk.Select(device => ToExpoMessage(device.Token, notification)).ToList();

                var response = await _httpClient.PostAsJsonAsync(ExpoPushEndpoint, messages);
                response.EnsureSuccessStatusCode();

                var payload = await response.Content.ReadFromJsonAsync<ExpoPushResponse>();
                var tickets = payload?.Data ?? Array.Empty<ExpoPushTicket>();

                // A Expo devolve um ticket por mensagem, na mesma ordem do envio.
                for (var i = 0; i < tickets.Length && i < chunk.Length; i++)
                {
                    var ticket = tickets[i];
                    if (ticket.Status == "ok")
                    {
                        successCount++;
                        firstMessageId ??= ticket.Id;
                        continue;
                    }

                    if (ticket.Message is not null)
                    {
                        errors.Add(ticket.Message);
                    }
                    if (ticket.Details?.Error == "DeviceNotRegistered")
                    {
                        deadDevices.Add(chunk[i]);
                    }
                }
            }

            await DeactivateAsync(deadDevices);

            _logger.LogInformation(
                "Push (Expo) enviado para {SuccessCount}/{Total} dispositivos",
                successCount, devices.Count);

            return new SendResult
            {
                Success = successCount > 0,
                MessageId = firstMessageId,
                ErrorMessage = errors.Count > 0 ? string.Join("; ", errors.Distinct()) : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Push notification failed");
            return new SendResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    // App desinstalada ou token revogado: a Expo responde DeviceNotRegistered e
    // continuar mandando só gera erro (e retry) para sempre.
    private async Task DeactivateAsync(List<DeviceToken> devices)
    {
        if (devices.Count == 0)
        {
            return;
        }

        foreach (var device in devices)
        {
            device.IsActive = false;
            await _deviceTokenRepo.UpdateAsync(device);
        }
        await _deviceTokenRepo.SaveChangesAsync();

        _logger.LogInformation("{Count} device token(s) desativado(s) por DeviceNotRegistered", devices.Count);
    }

    private static ExpoPushMessage ToExpoMessage(string token, Notification notification) => new()
    {
        To = token,
        Title = notification.Title,
        Body = notification.Message,
        Data = notification.Data.Count > 0 ? notification.Data : null,
        Sound = "default",
        Priority = notification.PushPriority switch
        {
            PushPriority.High => "high",
            PushPriority.Normal => "normal",
            PushPriority.Default => "default",
            _ => null
        },
        ChannelId = notification.PushChannelId,
        Ttl = notification.PushTtlSeconds
    };

    private class ExpoPushMessage
    {
        [JsonPropertyName("to")] public string To { get; set; } = string.Empty;
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("body")] public string? Body { get; set; }
        [JsonPropertyName("data")] public Dictionary<string, string>? Data { get; set; }
        [JsonPropertyName("sound")] public string? Sound { get; set; }

        [JsonPropertyName("priority")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Priority { get; set; }

        [JsonPropertyName("channelId")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ChannelId { get; set; }

        [JsonPropertyName("ttl")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Ttl { get; set; }
    }

    private class ExpoPushResponse
    {
        [JsonPropertyName("data")] public ExpoPushTicket[]? Data { get; set; }
    }

    private class ExpoPushTicket
    {
        [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("message")] public string? Message { get; set; }
        [JsonPropertyName("details")] public ExpoPushTicketDetails? Details { get; set; }
    }

    private class ExpoPushTicketDetails
    {
        [JsonPropertyName("error")] public string? Error { get; set; }
    }
}
