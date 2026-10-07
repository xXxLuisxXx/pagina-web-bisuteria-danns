using System.Linq.Expressions;
using Bisuteria.Application.Interfaces;
using Bisuteria.Domain.Common;
using Bisuteria.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bisuteria.Infrastructure.Repositories;

/// <summary>
/// Implementación genérica del repositorio sobre EF Core.
/// </summary>
public class EfRepository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly AppDbContext Context;
    protected readonly DbSet<T> Set;

    public EfRepository(AppDbContext context)
    {
        Context = context;
        Set = context.Set<T>();
    }

    public IQueryable<T> Query() => Set.AsQueryable();

    public Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(predicate, cancellationToken);

    public Task<List<T>> ListAsync(CancellationToken cancellationToken = default)
        => Set.ToListAsync(cancellationToken);

    public Task<List<T>> ListAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        => Set.Where(predicate).ToListAsync(cancellationToken);

    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        => Set.AnyAsync(predicate, cancellationToken);

    public Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default)
        => predicate is null ? Set.CountAsync(cancellationToken) : Set.CountAsync(predicate, cancellationToken);

    public async Task AddAsync(T entity, CancellationToken cancellationToken = default)
        => await Set.AddAsync(entity, cancellationToken);

    public void Update(T entity)
    {
        if (Context.Entry(entity).State == EntityState.Detached) Set.Update(entity);
    }

    public void Remove(T entity) => Set.Remove(entity);

    public void RemoveRange(IEnumerable<T> entities) => Set.RemoveRange(entities);
}
