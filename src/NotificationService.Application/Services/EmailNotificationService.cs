using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NotificationService.Application.DTOs;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Repositories;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace NotificationService.Application.Services;

public class EmailNotificationService : IEmailNotificationService
{
    private readonly ISendGridClient _sendGridClient;
    private readonly IUserRepository _userRepo;
    private readonly ILogger<EmailNotificationService> _logger;
    private readonly string _fromEmail;
    private readonly string _fromName;

    public EmailNotificationService(
        ISendGridClient sendGridClient,
        IUserRepository userRepo,
        ILogger<EmailNotificationService> logger,
        IConfiguration configuration)
    {
        _sendGridClient = sendGridClient;
        _userRepo = userRepo;
        _logger = logger;
        _fromEmail = configuration["SendGrid:FromEmail"] ?? "noreply@example.com";
        _fromName = configuration["SendGrid:FromName"] ?? "NotificationHub";
    }

    public async Task<SendResult> SendAsync(Notification notification)
    {
        try
        {
            var user = await _userRepo.GetByIdAsync(notification.UserId);
            if (string.IsNullOrEmpty(user?.Email))
            {
                return new SendResult { Success = false, ErrorMessage = "User email not found" };
            }

            var from = new EmailAddress(_fromEmail, _fromName);
            var to = new EmailAddress(user.Email);
            var imageTag = notification.ImageUrl is not null ? $"<img src=\"{notification.ImageUrl}\" />" : "";
            var htmlContent = $"<h2>{notification.Title}</h2><p>{notification.Message}</p>{imageTag}";

            var msg = MailHelper.CreateSingleEmail(from, to, notification.Title, notification.Message, htmlContent);
            var response = await _sendGridClient.SendEmailAsync(msg);

            _logger.LogInformation("Email sent to {Email}", user.Email);

            return new SendResult
            {
                Success = (int)response.StatusCode is >= 200 and < 300,
                MessageId = response.Headers.FirstOrDefault(h => h.Key == "X-Message-Id").Value?.FirstOrDefault()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Email notification failed");
            return new SendResult { Success = false, ErrorMessage = ex.Message };
        }
    }
}
