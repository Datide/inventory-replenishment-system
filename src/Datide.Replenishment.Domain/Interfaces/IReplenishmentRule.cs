namespace Datide.Replenishment.Domain.Interfaces;

using Datide.Replenishment.Domain.Entities;

/// <summary>
/// Contract for anything that can decide whether a SKU needs replenishment.
/// Kept as an interface so rules can be composed, replaced, or tested in isolation.
/// </summary>
public interface IReplenishmentRule
{
    /// <summary>
    /// Evaluates the SKU's recent stock history (oldest first).
    ///
    /// <paramref name="alreadyOrderedQuantity"/> is the quantity already on
    /// order but not yet received (in-flight POs). The suggestion is the
    /// *remaining* need after subtracting it, preventing double-ordering.
    /// Returns null when no further replenishment is warranted.
    /// </summary>
    ReplenishmentSuggestion? Evaluate(
        IReadOnlyList<StockSnapshot> history,
        double leadTimeFactor,
        int alreadyOrderedQuantity);
}
