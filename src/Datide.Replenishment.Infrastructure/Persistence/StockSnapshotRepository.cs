namespace Datide.Replenishment.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Datide.Replenishment.Application.Abstractions;
using Datide.Replenishment.Domain.Entities;

/// <summary>
/// EF Core implementation of <see cref="IStockSnapshotRepository"/>.
/// Reads real snapshot history from the database.
/// </summary>
public sealed class StockSnapshotRepository : IStockSnapshotRepository
{
    private readonly ReplenishmentDbContext _dbContext;

    public StockSnapshotRepository(ReplenishmentDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<IReadOnlyList<StockSnapshot>> GetRecentAsync(
        string sku,
        int days,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        ArgumentOutOfRangeException.ThrowIfLessThan(days, 1);

        return await _dbContext.StockSnapshots
            .AsNoTracking()
            .Where(s => s.Sku == sku)
            .OrderByDescending(s => s.SnapshotDate)
            .Take(days)
            .OrderBy(s => s.SnapshotDate) // oldest first, as the rule expects
            .ToListAsync(cancellationToken);
    }
}
