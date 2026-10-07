using Bisuteria.Domain.Common;

namespace Bisuteria.Application.Interfaces;

/// <summary>
/// Unidad de trabajo: agrupa repositorios y consolida los cambios en una sola transacción.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    IRepository<T> Repository<T>() where T : BaseEntity;

    Task<T> InTransactionAsync<T>(Func<Task<T>> operation, CancellationToken ct = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
