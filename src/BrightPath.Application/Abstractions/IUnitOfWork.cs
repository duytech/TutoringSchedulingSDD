namespace BrightPath.Application.Abstractions;

/// <summary>Saves what the repositories changed, inside a transaction when one is open.</summary>
public interface IUnitOfWork
{
    /// <summary>Disposing the transaction without committing it rolls it back.</summary>
    Task<ITransaction> BeginTransactionAsync(CancellationToken ct);

    /// <summary>Throws <see cref="SlotTakenException"/> when the database refuses an overlapping slot.</summary>
    Task SaveChangesAsync(CancellationToken ct);
}

public interface ITransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct);
}
