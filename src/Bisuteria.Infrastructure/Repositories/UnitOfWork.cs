using Microsoft.EntityFrameworkCore;
using System.Data;
using Bisuteria.Application.Interfaces;
using Bisuteria.Domain.Common;
using Bisuteria.Infrastructure.Data;
using Bisuteria.Infrastructure.Repositories;

namespace Bisuteria.Infrastructure.Repositories;

/// <summary>
/// Unidad de trabajo sobre EF Core: comparte el contexto para que todos los
/// repositorios operen dentro de una misma transacción.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private readonly Dictionary<Type, object> _repositories = new();

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    public IRepository<T> Repository<T>() where T : BaseEntity
    {
        var type = typeof(T);
        if (!_repositories.TryGetValue(type, out var repository))
        {
            repository = new EfRepository<T>(_context);
            _repositories[type] = repository;
        }

        return (IRepository<T>)repository;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);

    public async Task<T> InTransactionAsync<T>(Func<Task<T>> operation, CancellationToken ct = default)
    {
        if (_context.Database.CurrentTransaction is not null) return await operation();
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var result = await operation();
            await transaction.CommitAsync(ct);
            return result;
        });
    }

    public void Dispose() => _context.Dispose();
}
