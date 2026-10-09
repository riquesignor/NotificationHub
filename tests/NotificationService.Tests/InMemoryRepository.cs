using System.Linq.Expressions;
using NotificationService.Domain.Repositories;

namespace NotificationService.Tests;

// Repositório em memória para os testes: aplica o mesmo predicado que o EF
// aplicaria, sem banco. Entidades precisam ter uma propriedade Guid "Id".
public class InMemoryRepository<T> : IRepository<T> where T : class
{
    public List<T> Items { get; } = new();
    public int SaveCount { get; private set; }

    public Task<T?> GetByIdAsync(Guid id)
        => Task.FromResult(Items.FirstOrDefault(i => (Guid)typeof(T).GetProperty("Id")!.GetValue(i)! == id));

    public Task<IEnumerable<T>> GetAsync(Expression<Func<T, bool>> predicate)
        => Task.FromResult<IEnumerable<T>>(Items.Where(predicate.Compile()).ToList());

    public Task AddAsync(T entity)
    {
        Items.Add(entity);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(T entity) => Task.CompletedTask;

    public Task SaveChangesAsync()
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}
