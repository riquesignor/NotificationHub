using NotificationService.Domain.Entities;

namespace NotificationService.Domain.Repositories;

public interface ITemplateRepository : IRepository<NotificationTemplate>
{
    Task<NotificationTemplate?> GetByNameAsync(string appId, string name);
    Task<IEnumerable<NotificationTemplate>> GetAllAsync(string appId);
}
