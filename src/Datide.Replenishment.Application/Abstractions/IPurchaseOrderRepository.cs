namespace Datide.Replenishment.Application.Abstractions;

using Datide.Replenishment.Domain.Entities;

/// <summary>
/// Data-access contract for purchase orders, used by the replenishment logic
/// to know how much stock is already on order but not yet received.
/// </summary>
public interface IPurchaseOrderRepository
{
    /// <summary>
    /// Total quantity of in-flight (pending or approved, not yet received)
    /// purchase orders for the given SKU. Used to avoid double-ordering.
    /// </summary>
    Task<int> GetInFlightQuantityAsync(
        string sku,
        CancellationToken cancellationToken = default);
}
