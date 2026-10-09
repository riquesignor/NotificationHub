using Microsoft.EntityFrameworkCore;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Repositories;
using NotificationService.Infrastructure.Data;

namespace NotificationService.Infrastructure.Repositories;

public class TemplateRepository : Repository<NotificationTemplate>, ITemplateRepository
{
    public TemplateRepository(AppDbContext context) : base(context)
    {
    }

    public Task<NotificationTemplate?> GetByNameAsync(string appId, string name)
        => DbSet.FirstOrDefaultAsync(t => t.AppId == appId && t.Name == name);

    public async Task<IEnumerable<NotificationTemplate>> GetAllAsync(string appId)
        => await DbSet.Where(t => t.AppId == appId).ToListAsync();
}
