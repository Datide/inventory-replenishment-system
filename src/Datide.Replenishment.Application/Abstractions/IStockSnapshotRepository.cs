namespace Datide.Replenishment.Application.Abstractions;

using Datide.Replenishment.Domain.Entities;

/// <summary>
/// Data-access contract for reading stock history. Implemented by the
/// Infrastructure layer (e.g. EF Core / Dapper); the Application layer only
/// depends on this abstraction so it stays unit-testable.
/// </summary>
public interface IStockSnapshotRepository
{
    /// <summary>
    /// Returns up to <paramref name="days"/> of daily snapshots for a SKU,
    /// ordered oldest first.
    /// </summary>
    Task<IReadOnlyList<StockSnapshot>> GetRecentAsync(
        string sku,
        int days,
        CancellationToken cancellationToken = default);
}
