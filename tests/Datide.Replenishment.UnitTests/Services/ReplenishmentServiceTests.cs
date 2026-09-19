namespace Datide.Replenishment.UnitTests.Services;

using Datide.Replenishment.Application.Abstractions;
using Datide.Replenishment.Application.Contracts;
using Datide.Replenishment.Application.Services;
using Datide.Replenishment.Domain.Entities;
using Datide.Replenishment.Domain.Rules;

/// <summary>
/// Tests for <see cref="ReplenishmentService"/> using in-memory fakes —
/// no database required.
/// </summary>
public sealed class ReplenishmentServiceTests
{
    private sealed class FakeStockRepository : IStockSnapshotRepository
    {
        private readonly IReadOnlyList<StockSnapshot> _snapshots;

        public FakeStockRepository(IReadOnlyList<StockSnapshot> snapshots) => _snapshots = snapshots;

        public Task<IReadOnlyList<StockSnapshot>> GetRecentAsync(
            string sku,
            int days,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_snapshots);
    }

    private sealed class FakePoRepository : IPurchaseOrderRepository
    {
        private readonly int _inFlight;

        public FakePoRepository(int inFlight) => _inFlight = inFlight;

        public Task<int> GetInFlightQuantityAsync(
            string sku,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_inFlight);
    }

    private static ReplenishmentService Build(
        IReadOnlyList<StockSnapshot> snapshots,
        int inFlight = 0)
        => new(
            new FakeStockRepository(snapshots),
            new FakePoRepository(inFlight),
            new ConsecutiveDaysReplenishmentRule(requiredConsecutiveDays: 2));

    [Fact]
    public async Task EvaluateAsync_WhenRuleSuggests_ReturnsEvaluationResult()
    {
        // Arrange
        var snapshots = new List<StockSnapshot>
        {
            new("SKU-2001", currentStock: 10, reorderPoint: 50, maxStock: 200, new DateOnly(2026, 9, 1)),
            new("SKU-2001", currentStock: 10, reorderPoint: 50, maxStock: 200, new DateOnly(2026, 9, 2)),
        };
        var service = Build(snapshots);

        // Act
        EvaluationResult? result = await service.EvaluateAsync(
            new EvaluateSkuRequest("SKU-2001", LookbackDays: 7));

        // Assert
        Assert.NotNull(result);
        Assert.Equal("SKU-2001", result.Sku);
        Assert.Equal(190, result.SuggestedQuantity);
        Assert.Equal(0, result.AlreadyOnOrder);
        Assert.Equal(190, result.GrossNeed);
    }

    [Fact]
    public async Task EvaluateAsync_InFlightStockReducesSuggestion()
    {
        // Arrange — gross need 190, but 50 already on order → suggest 140.
        var snapshots = new List<StockSnapshot>
        {
            new("SKU-2001", currentStock: 10, reorderPoint: 50, maxStock: 200, new DateOnly(2026, 9, 1)),
            new("SKU-2001", currentStock: 10, reorderPoint: 50, maxStock: 200, new DateOnly(2026, 9, 2)),
        };
        var service = Build(snapshots, inFlight: 50);

        // Act
        EvaluationResult? result = await service.EvaluateAsync(
            new EvaluateSkuRequest("SKU-2001", LookbackDays: 7));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(140, result.SuggestedQuantity);
        Assert.Equal(50, result.AlreadyOnOrder);
        Assert.Equal(190, result.GrossNeed);
    }

    [Fact]
    public async Task EvaluateAsync_InFlightCoversNeed_ReturnsNull()
    {
        // Arrange — 200 in-flight ≥ 190 gross need → nothing to order.
        var snapshots = new List<StockSnapshot>
        {
            new("SKU-2001", currentStock: 10, reorderPoint: 50, maxStock: 200, new DateOnly(2026, 9, 1)),
            new("SKU-2001", currentStock: 10, reorderPoint: 50, maxStock: 200, new DateOnly(2026, 9, 2)),
        };
        var service = Build(snapshots, inFlight: 200);

        // Act
        EvaluationResult? result = await service.EvaluateAsync(
            new EvaluateSkuRequest("SKU-2001", LookbackDays: 7));

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task EvaluateAsync_WhenRuleSilent_ReturnsNull()
    {
        // Arrange — stock recovered above reorder point.
        var snapshots = new List<StockSnapshot>
        {
            new("SKU-2002", currentStock: 10, reorderPoint: 50, maxStock: 200, new DateOnly(2026, 9, 1)),
            new("SKU-2002", currentStock: 120, reorderPoint: 50, maxStock: 200, new DateOnly(2026, 9, 2)),
        };
        var service = Build(snapshots);

        // Act
        EvaluationResult? result = await service.EvaluateAsync(
            new EvaluateSkuRequest("SKU-2002", LookbackDays: 7));

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task EvaluateAsync_HighValueOrder_RequiresManagerApproval()
    {
        // Arrange — large max stock produces a suggestion above the threshold.
        var snapshots = new List<StockSnapshot>
        {
            new("SKU-2003", currentStock: 10, reorderPoint: 50, maxStock: 10_000, new DateOnly(2026, 9, 1)),
            new("SKU-2003", currentStock: 10, reorderPoint: 50, maxStock: 10_000, new DateOnly(2026, 9, 2)),
        };
        var service = Build(snapshots);

        // Act
        EvaluationResult? result = await service.EvaluateAsync(
            new EvaluateSkuRequest("SKU-2003", LookbackDays: 7));

        // Assert — (10000-10) * 25 = ~249,750 > 50,000 threshold.
        Assert.NotNull(result);
        Assert.True(result.RequiresManagerApproval);
    }
}
