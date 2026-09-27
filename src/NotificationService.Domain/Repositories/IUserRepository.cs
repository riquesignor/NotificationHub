using NotificationService.Domain.Entities;

namespace NotificationService.Domain.Repositories;

// Dedicado porque User.Id é a string externa da app consumidora
// (ex.: UID do Firebase Auth), não um Guid como as outras entidades —
// não cabe no IRepository<T> genérico (que assume Guid).
public interface IUserRepository
{
    Task<User?> GetByIdAsync(string id);
    Task AddAsync(User user);
    Task UpdateAsync(User user);
    Task SaveChangesAsync();
}
