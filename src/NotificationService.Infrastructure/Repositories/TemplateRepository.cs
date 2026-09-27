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

    public Task<NotificationTemplate?> GetByNameAsync(string name)
        => DbSet.FirstOrDefaultAsync(t => t.Name == name);

    public async Task<IEnumerable<NotificationTemplate>> GetAllAsync()
        => await DbSet.ToListAsync();
}
