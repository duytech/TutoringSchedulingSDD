using BrightPath.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace BrightPath.Infrastructure.Persistence;

internal sealed class EfUnitOfWork(BrightPathDbContext db) : IUnitOfWork
{
    public async Task<ITransaction> BeginTransactionAsync(CancellationToken ct) =>
        new EfTransaction(await db.Database.BeginTransactionAsync(ct));

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.ExclusionViolation,
        } pg)
        {
            throw new SlotTakenException(SlotOf(pg.ConstraintName), ex);
        }
    }

    /// <summary>The exclusion constraints from the <c>ExclusionConstraints</c> migration.</summary>
    private static SlotKind SlotOf(string? constraint) => constraint switch
    {
        "ex_sessions_room_slot" => SlotKind.Room,
        "ex_sessions_tutor_slot" => SlotKind.Tutor,
        _ => SlotKind.Student,
    };

    private sealed class EfTransaction(IDbContextTransaction transaction) : ITransaction
    {
        public Task CommitAsync(CancellationToken ct) => transaction.CommitAsync(ct);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
