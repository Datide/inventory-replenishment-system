namespace Datide.Replenishment.Domain.Rules;

using Datide.Replenishment.Domain.Entities;
using Datide.Replenishment.Domain.Interfaces;

/// <summary>
/// Consecutive-days replenishment rule.
///
/// Business rule: a suggestion is generated only when a SKU sits at or below
/// its reorder point for <see cref="RequiredConsecutiveDays"/> consecutive daily
/// snapshots. This deliberately dampens one-day dips (e.g. a single large order
/// temporarily draining stock) from triggering unnecessary purchases.
///
/// The suggested quantity is the *remaining* need: the gross target
/// (top-up toward max stock) minus the quantity already on order but not yet
/// received. If in-flight stock already covers the need, no suggestion is made.
/// </summary>
public sealed class ConsecutiveDaysReplenishmentRule : IReplenishmentRule
{
    private readonly int _requiredConsecutiveDays;

    public ConsecutiveDaysReplenishmentRule(int requiredConsecutiveDays)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(requiredConsecutiveDays, 1);
        _requiredConsecutiveDays = requiredConsecutiveDays;
    }

    public ReplenishmentSuggestion? Evaluate(
        IReadOnlyList<StockSnapshot> history,
        double leadTimeFactor,
        int alreadyOrderedQuantity)
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentOutOfRangeException.ThrowIfNegative(alreadyOrderedQuantity);

        if (history.Count == 0)
        {
            return null;
        }

        // Walk from most recent snapshot backwards; break the streak as soon as
        // a snapshot is above the reorder point.
        int consecutiveDays = 0;
        foreach (StockSnapshot snapshot in history.OrderByDescending(h => h.SnapshotDate))
        {
            if (!snapshot.IsBelowReorderPoint)
            {
                break;
            }

            consecutiveDays++;
            if (consecutiveDays >= _requiredConsecutiveDays)
            {
                int grossNeed = CalculateGrossTarget(snapshot, leadTimeFactor);
                int remainingNeed = Math.Max(grossNeed - alreadyOrderedQuantity, 0);

                if (remainingNeed == 0)
                {
                    // In-flight POs already cover the need — no double-ordering.
                    return null;
                }

                return new ReplenishmentSuggestion(
                    sku: snapshot.Sku,
                    suggestedQuantity: remainingNeed,
                    currentStock: snapshot.CurrentStock,
                    reorderPoint: snapshot.ReorderPoint,
                    consecutiveDays: consecutiveDays,
                    grossNeed: grossNeed,
                    alreadyOnOrder: alreadyOrderedQuantity);
            }
        }

        return null;
    }

    private static int CalculateGrossTarget(StockSnapshot snapshot, double leadTimeFactor)
    {
        // Top up from current stock toward max stock, scaled by supplier
        // lead-time variability. Rounded up so a fractional need still results
        // in a purchasable whole-unit quantity.
        double target = (snapshot.MaxStock - snapshot.CurrentStock) * leadTimeFactor;
        return (int)Math.Ceiling(target);
    }
}
