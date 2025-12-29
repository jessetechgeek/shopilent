namespace Shopilent.Application.Abstractions.Persistence;

public interface IUnitOfWork
{
    Task<int> CommitAsync(CancellationToken cancellationToken = default);
}
