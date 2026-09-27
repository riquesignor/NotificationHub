using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using NotificationService.Application.DTOs;
using NotificationService.Application.Services;

namespace NotificationService.API.Controllers;

[ApiController]
[Route("api/notifications")]
public class NotificationController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly IValidator<NotificationRequest> _requestValidator;
    private readonly IValidator<TemplateNotificationRequest> _templateRequestValidator;

    public NotificationController(
        INotificationService notificationService,
        IValidator<NotificationRequest> requestValidator,
        IValidator<TemplateNotificationRequest> templateRequestValidator)
    {
        _notificationService = notificationService;
        _requestValidator = requestValidator;
        _templateRequestValidator = templateRequestValidator;
    }

    [HttpPost("send")]
    public async Task<IActionResult> Send(NotificationRequest request)
    {
        var validation = await _requestValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationErrors(validation);
        }

        var result = await _notificationService.SendAsync(request);
        return Ok(result);
    }

    [HttpPost("send-template")]
    public async Task<IActionResult> SendTemplate(TemplateNotificationRequest request)
    {
        var validation = await _templateRequestValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationErrors(validation);
        }

        var result = await _notificationService.SendFromTemplateAsync(request);
        return Ok(result);
    }

    [HttpGet("{id:guid}/status")]
    public async Task<IActionResult> GetStatus(Guid id)
    {
        var status = await _notificationService.GetStatusAsync(id);
        if (status is null)
        {
            return NotFound();
        }

        var logs = await _notificationService.GetLogsAsync(id);
        return Ok(new { id, status, logs });
    }

    private IActionResult ValidationErrors(ValidationResult validation)
    {
        var errors = validation.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

        return ValidationProblem(new ValidationProblemDetails(errors));
    }
}
