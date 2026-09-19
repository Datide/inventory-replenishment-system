namespace Datide.Replenishment.Domain.Entities;

/// <summary>
/// The result of evaluating replenishment rules for a single SKU.
/// A null suggestion (from the rules engine) means "no action needed".
///
/// Carries *structured* facts only — no human-readable sentence — so the
/// presentation layer can localise the reason without parsing English text.
/// </summary>
public sealed class ReplenishmentSuggestion
{
    public ReplenishmentSuggestion(
        string sku,
        int suggestedQuantity,
        int currentStock,
        int reorderPoint,
        int consecutiveDays,
        int grossNeed,
        int alreadyOnOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        ArgumentOutOfRangeException.ThrowIfNegative(suggestedQuantity);
        ArgumentOutOfRangeException.ThrowIfNegative(currentStock);
        ArgumentOutOfRangeException.ThrowIfNegative(reorderPoint);
        ArgumentOutOfRangeException.ThrowIfLessThan(consecutiveDays, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(grossNeed);
        ArgumentOutOfRangeException.ThrowIfNegative(alreadyOnOrder);

        Sku = sku;
        SuggestedQuantity = suggestedQuantity;
        CurrentStock = currentStock;
        ReorderPoint = reorderPoint;
        ConsecutiveDays = consecutiveDays;
        GrossNeed = grossNeed;
        AlreadyOnOrder = alreadyOnOrder;
    }

    public string Sku { get; }

    /// <summary>Remaining quantity still needed after in-flight stock.</summary>
    public int SuggestedQuantity { get; }

    public int CurrentStock { get; }

    public int ReorderPoint { get; }

    public int ConsecutiveDays { get; }

    /// <summary>Target top-up quantity before subtracting in-flight stock.</summary>
    public int GrossNeed { get; }

    public int AlreadyOnOrder { get; }
}
