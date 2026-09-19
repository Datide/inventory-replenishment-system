namespace Datide.Replenishment.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Datide.Replenishment.Application.Abstractions;
using Datide.Replenishment.Domain.Entities;

/// <summary>
/// EF Core implementation of <see cref="IPurchaseOrderRepository"/>.
/// In-flight quantity = sum of quantities on draft, pending or approved POs
/// (planned or ordered but not yet received into stock).
/// </summary>
public sealed class PurchaseOrderRepository : IPurchaseOrderRepository
{
    private readonly ReplenishmentDbContext _dbContext;

    public PurchaseOrderRepository(ReplenishmentDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<int> GetInFlightQuantityAsync(
        string sku,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);

        return await _dbContext.PurchaseOrders
            .AsNoTracking()
            .Where(p => p.SkuCode == sku
                        && (p.Status == PurchaseOrderStatus.Draft
                            || p.Status == PurchaseOrderStatus.Pending
                            || p.Status == PurchaseOrderStatus.Approved))
            .SumAsync(p => (int?)p.Quantity, cancellationToken) ?? 0;
    }
}
