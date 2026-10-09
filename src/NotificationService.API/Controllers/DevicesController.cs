using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using NotificationService.API.Security;
using NotificationService.Application.DTOs;
using NotificationService.Application.Security;
using NotificationService.Application.Services;

namespace NotificationService.API.Controllers;

[ApiController]
[Route("api/devices")]
public class DevicesController : ControllerBase
{
    private readonly IDeviceService _deviceService;
    private readonly IValidator<RegisterDeviceRequest> _registerValidator;
    private readonly IValidator<UnregisterDeviceRequest> _unregisterValidator;

    public DevicesController(
        IDeviceService deviceService,
        IValidator<RegisterDeviceRequest> registerValidator,
        IValidator<UnregisterDeviceRequest> unregisterValidator)
    {
        _deviceService = deviceService;
        _registerValidator = registerValidator;
        _unregisterValidator = unregisterValidator;
    }

    // Chave pública basta: é chamado direto do app no aparelho.
    [HttpPost("register")]
    [ApiKeyAuth(ApiKeyScope.Public)]
    public async Task<IActionResult> Register(RegisterDeviceRequest request)
    {
        var validation = await _registerValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationErrors(validation);
        }

        var device = await _deviceService.RegisterAsync(HttpContext.GetClientApp().Id, request);
        return Ok(new
        {
            id = device.Id,
            userId = device.UserId,
            deviceType = device.DeviceType,
            deviceName = device.DeviceName,
            attributes = device.Attributes,
            isActive = device.IsActive,
            registeredAt = device.RegisteredAt,
            lastUsedAt = device.LastUsedAt
        });
    }

    // Ex.: usuário desligou as notificações ou saiu da conta.
    [HttpPost("unregister")]
    [ApiKeyAuth(ApiKeyScope.Public)]
    public async Task<IActionResult> Unregister(UnregisterDeviceRequest request)
    {
        var validation = await _unregisterValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationErrors(validation);
        }

        var removed = await _deviceService.UnregisterAsync(HttpContext.GetClientApp().Id, request.Token);
        return removed ? NoContent() : NotFound();
    }

    /// <summary>
    /// Lista aparelhos ativos da app. Filtros: ?userId=...&amp;attr[chave]=valor
    /// (igualdade exata, todos precisam bater) e paginação ?page=&amp;pageSize= (máx. 500).
    /// </summary>
    [HttpGet]
    [ApiKeyAuth(ApiKeyScope.Secret)]
    public async Task<IActionResult> List(
        [FromQuery] string? userId,
        [FromQuery(Name = "attr")] Dictionary<string, string>? attributes,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100)
    {
        var result = await _deviceService.ListAsync(HttpContext.GetClientApp().Id, new DeviceQuery
        {
            UserId = userId,
            Attributes = attributes ?? new Dictionary<string, string>(),
            Page = page,
            PageSize = pageSize
        });
        return Ok(result);
    }

    private IActionResult ValidationErrors(ValidationResult validation)
    {
        var errors = validation.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

        return ValidationProblem(new ValidationProblemDetails(errors));
    }
}
