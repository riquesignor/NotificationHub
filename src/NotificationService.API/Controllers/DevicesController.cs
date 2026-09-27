using Microsoft.AspNetCore.Mvc;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using NotificationService.Domain.Repositories;

namespace NotificationService.API.Controllers;

[ApiController]
[Route("api/devices")]
public class DevicesController : ControllerBase
{
    private readonly IRepository<DeviceToken> _deviceTokenRepository;

    public DevicesController(IRepository<DeviceToken> deviceTokenRepository)
    {
        _deviceTokenRepository = deviceTokenRepository;
    }

    public record RegisterDeviceRequest(Guid UserId, string Token, DeviceType DeviceType, string? DeviceName);

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDeviceRequest request)
    {
        var existing = (await _deviceTokenRepository.GetAsync(
            d => d.UserId == request.UserId && d.Token == request.Token)).FirstOrDefault();

        if (existing is not null)
        {
            existing.IsActive = true;
            existing.LastUsedAt = DateTime.UtcNow;
            existing.DeviceName = request.DeviceName;
            await _deviceTokenRepository.UpdateAsync(existing);
            await _deviceTokenRepository.SaveChangesAsync();
            return Ok(existing);
        }

        var deviceToken = new DeviceToken
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            Token = request.Token,
            DeviceType = request.DeviceType,
            DeviceName = request.DeviceName,
            IsActive = true,
            RegisteredAt = DateTime.UtcNow
        };

        await _deviceTokenRepository.AddAsync(deviceToken);
        await _deviceTokenRepository.SaveChangesAsync();

        return Ok(deviceToken);
    }
}
