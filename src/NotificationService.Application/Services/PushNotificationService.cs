using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using NotificationService.Application.DTOs;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Repositories;

namespace NotificationService.Application.Services;

// Envia via Expo Push API em vez de Firebase Admin SDK direto: os apps
// consumidores (Expo/React Native) coletam ExponentPushToken[...], não
// token nativo de FCM, e a Expo Push API não pede nenhuma credencial.
public class PushNotificationService : IPushNotificationService
{
    private const string ExpoPushEndpoint = "https://exp.host/--/api/v2/push/send";

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
            var tokens = await _deviceTokenRepo.GetAsync(
                dt => dt.UserId == notification.UserId && dt.IsActive);

            var tokenList = tokens.Select(t => t.Token).ToList();
            if (tokenList.Count == 0)
            {
                return new SendResult { Success = false, ErrorMessage = "No active device tokens" };
            }

            var messages = tokenList.Select(token => new ExpoPushMessage
            {
                To = token,
                Title = notification.Title,
                Body = notification.Message,
                Data = notification.Data,
                Sound = "default"
            });

            var response = await _httpClient.PostAsJsonAsync(ExpoPushEndpoint, messages);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<ExpoPushResponse>();
            var receipts = payload?.Data ?? Array.Empty<ExpoPushReceipt>();

            var successCount = receipts.Count(r => r.Status == "ok");
            var errors = receipts.Where(r => r.Status != "ok" && r.Message is not null)
                .Select(r => r.Message!)
                .ToList();

            _logger.LogInformation(
                "Push (Expo) enviado para {SuccessCount}/{Total} dispositivos",
                successCount, tokenList.Count);

            return new SendResult
            {
                Success = successCount > 0,
                MessageId = receipts.FirstOrDefault(r => r.Status == "ok")?.Id,
                ErrorMessage = errors.Count > 0 ? string.Join("; ", errors) : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Push notification failed");
            return new SendResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private class ExpoPushMessage
    {
        [JsonPropertyName("to")] public string To { get; set; } = string.Empty;
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("body")] public string? Body { get; set; }
        [JsonPropertyName("data")] public Dictionary<string, string>? Data { get; set; }
        [JsonPropertyName("sound")] public string? Sound { get; set; }
    }

    private class ExpoPushResponse
    {
        [JsonPropertyName("data")] public ExpoPushReceipt[]? Data { get; set; }
    }

    private class ExpoPushReceipt
    {
        [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("message")] public string? Message { get; set; }
    }
}
