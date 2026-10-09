using NotificationService.Application.DTOs;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Repositories;

namespace NotificationService.Application.Services;

public interface IDeviceService
{
    Task<DeviceToken> RegisterAsync(string appId, RegisterDeviceRequest request);
    Task<bool> UnregisterAsync(string appId, string token);
    Task<PagedResult<DeviceSummary>> ListAsync(string appId, DeviceQuery query);
}

public class DeviceService : IDeviceService
{
    public const int MaxPageSize = 500;

    private readonly IRepository<DeviceToken> _deviceTokenRepo;

    public DeviceService(IRepository<DeviceToken> deviceTokenRepo)
    {
        _deviceTokenRepo = deviceTokenRepo;
    }

    public async Task<DeviceToken> RegisterAsync(string appId, RegisterDeviceRequest request)
    {
        // Um token de push identifica um aparelho/instalação: se ele reaparece
        // com outro UserId (troca de conta no mesmo celular), é o mesmo registro.
        var existing = (await _deviceTokenRepo.GetAsync(
            d => d.AppId == appId && d.Token == request.Token)).FirstOrDefault();

        var attributes = request.Attributes ?? new Dictionary<string, string>();

        if (existing is not null)
        {
            existing.UserId = request.UserId;
            existing.DeviceType = request.DeviceType;
            existing.DeviceName = request.DeviceName;
            existing.Attributes = attributes;
            existing.IsActive = true;
            existing.LastUsedAt = DateTime.UtcNow;
            await _deviceTokenRepo.UpdateAsync(existing);
            await _deviceTokenRepo.SaveChangesAsync();
            return existing;
        }

        var deviceToken = new DeviceToken
        {
            Id = Guid.NewGuid(),
            AppId = appId,
            UserId = request.UserId,
            Token = request.Token,
            DeviceType = request.DeviceType,
            DeviceName = request.DeviceName,
            Attributes = attributes,
            IsActive = true,
            RegisteredAt = DateTime.UtcNow
        };

        await _deviceTokenRepo.AddAsync(deviceToken);
        await _deviceTokenRepo.SaveChangesAsync();
        return deviceToken;
    }

    public async Task<bool> UnregisterAsync(string appId, string token)
    {
        var existing = (await _deviceTokenRepo.GetAsync(
            d => d.AppId == appId && d.Token == token && d.IsActive)).FirstOrDefault();
        if (existing is null)
        {
            return false;
        }

        existing.IsActive = false;
        await _deviceTokenRepo.UpdateAsync(existing);
        await _deviceTokenRepo.SaveChangesAsync();
        return true;
    }

    public async Task<PagedResult<DeviceSummary>> ListAsync(string appId, DeviceQuery query)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        var devices = await _deviceTokenRepo.GetAsync(d =>
            d.AppId == appId && d.IsActive && (query.UserId == null || d.UserId == query.UserId));

        // Atributos são guardados como JSON numa coluna texto, então o filtro
        // por atributo roda em memória. Suficiente para milhares de aparelhos
        // por app; se crescer muito, o próximo passo é jsonb + índice GIN.
        var matching = devices
            .Where(d => query.Attributes.All(f =>
                d.Attributes.TryGetValue(f.Key, out var value) && value == f.Value))
            .OrderBy(d => d.RegisteredAt)
            .ThenBy(d => d.Id)
            .ToList();

        var items = matching
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new DeviceSummary(d.UserId, d.DeviceType, d.DeviceName, d.Attributes, d.RegisteredAt, d.LastUsedAt))
            .ToList();

        return new PagedResult<DeviceSummary>(items, page, pageSize, matching.Count);
    }
}
