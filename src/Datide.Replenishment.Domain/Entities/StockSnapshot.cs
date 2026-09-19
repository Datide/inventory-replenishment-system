namespace Datide.Replenishment.Domain.Entities;

/// <summary>
/// A daily stock position for a single SKU, used as input to the
/// replenishment rules engine. Public surface is read-only; values are
/// set through the constructor (or by EF Core materialisation).
/// </summary>
public sealed class StockSnapshot
{
    // Parameterless constructor for EF Core materialisation.
    private StockSnapshot()
    {
        Sku = string.Empty;
    }

    public StockSnapshot(string sku, int currentStock, int reorderPoint, int maxStock, DateOnly snapshotDate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        ArgumentOutOfRangeException.ThrowIfNegative(currentStock);
        ArgumentOutOfRangeException.ThrowIfNegative(reorderPoint);
        ArgumentOutOfRangeException.ThrowIfNegative(maxStock);

        Sku = sku;
        CurrentStock = currentStock;
        ReorderPoint = reorderPoint;
        MaxStock = maxStock;
        SnapshotDate = snapshotDate;
    }

    public string Sku { get; private set; }
    public int CurrentStock { get; private set; }
    public int ReorderPoint { get; private set; }
    public int MaxStock { get; private set; }
    public DateOnly SnapshotDate { get; private set; }

    /// <summary>True when the SKU is at or below its reorder point on this snapshot.</summary>
    public bool IsBelowReorderPoint => CurrentStock <= ReorderPoint;
}
