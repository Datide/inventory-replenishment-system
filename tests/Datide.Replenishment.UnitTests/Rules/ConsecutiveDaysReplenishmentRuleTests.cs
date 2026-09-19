namespace Datide.Replenishment.UnitTests.Rules;

using Datide.Replenishment.Domain.Entities;
using Datide.Replenishment.Domain.Rules;

/// <summary>
/// Tests for the consecutive-days replenishment rule — the core business logic.
/// </summary>
public sealed class ConsecutiveDaysReplenishmentRuleTests
{
    private static StockSnapshot Snapshot(string sku, DateOnly date, int stock, int reorder, int max)
        => new(sku, stock, reorder, max, date);

    [Fact]
    public void Evaluate_StockBelowThresholdForRequiredDays_ReturnsSuggestion()
    {
        // Arrange
        var rule = new ConsecutiveDaysReplenishmentRule(requiredConsecutiveDays: 2);
        var history = new List<StockSnapshot>
        {
            Snapshot("SKU-1001", new DateOnly(2026, 9, 1), stock: 10, reorder: 50, max: 200),
            Snapshot("SKU-1001", new DateOnly(2026, 9, 2), stock: 10, reorder: 50, max: 200),
        };

        // Act
        var result = rule.Evaluate(history, leadTimeFactor: 1.0, alreadyOrderedQuantity: 0);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("SKU-1001", result.Sku);
        Assert.Equal(190, result.SuggestedQuantity); // (200 - 10) * 1.0
    }

    [Fact]
    public void Evaluate_InFlightStockReducesSuggestedQuantity()
    {
        // Arrange — gross need is 190, but 50 are already on order.
        var rule = new ConsecutiveDaysReplenishmentRule(requiredConsecutiveDays: 2);
        var history = new List<StockSnapshot>
        {
            Snapshot("SKU-1001", new DateOnly(2026, 9, 1), stock: 10, reorder: 50, max: 200),
            Snapshot("SKU-1001", new DateOnly(2026, 9, 2), stock: 10, reorder: 50, max: 200),
        };

        // Act
        var result = rule.Evaluate(history, leadTimeFactor: 1.0, alreadyOrderedQuantity: 50);

        // Assert — 190 gross − 50 in-flight = 140 remaining.
        Assert.NotNull(result);
        Assert.Equal(140, result.SuggestedQuantity);
        // Structured facts, not a pre-built sentence.
        Assert.Equal(190, result.GrossNeed);
        Assert.Equal(50, result.AlreadyOnOrder);
        Assert.Equal(10, result.CurrentStock);
        Assert.Equal(50, result.ReorderPoint);
        Assert.Equal(2, result.ConsecutiveDays);
    }

    [Fact]
    public void Evaluate_InFlightStockFullyCoversNeed_ReturnsNull()
    {
        // Arrange — in-flight 200 ≥ gross need 190 → no double-ordering.
        var rule = new ConsecutiveDaysReplenishmentRule(requiredConsecutiveDays: 2);
        var history = new List<StockSnapshot>
        {
            Snapshot("SKU-1001", new DateOnly(2026, 9, 1), stock: 10, reorder: 50, max: 200),
            Snapshot("SKU-1001", new DateOnly(2026, 9, 2), stock: 10, reorder: 50, max: 200),
        };

        // Act
        var result = rule.Evaluate(history, leadTimeFactor: 1.0, alreadyOrderedQuantity: 200);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Evaluate_InFlightStockExactlyCoversNeed_ReturnsNull()
    {
        // Arrange — in-flight exactly equals gross need → nothing more to order.
        var rule = new ConsecutiveDaysReplenishmentRule(requiredConsecutiveDays: 1);
        var history = new List<StockSnapshot>
        {
            Snapshot("SKU-1005", new DateOnly(2026, 9, 1), stock: 10, reorder: 50, max: 200),
        };

        // Act — gross = 190, in-flight = 190.
        var result = rule.Evaluate(history, leadTimeFactor: 1.0, alreadyOrderedQuantity: 190);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Evaluate_SingleDayDipThenRecovery_ReturnsNull()
    {
        // Arrange — the dampening scenario from the UAT plan (UAT-02).
        var rule = new ConsecutiveDaysReplenishmentRule(requiredConsecutiveDays: 2);
        var history = new List<StockSnapshot>
        {
            Snapshot("SKU-1002", new DateOnly(2026, 9, 1), stock: 10, reorder: 50, max: 200),  // dip
            Snapshot("SKU-1002", new DateOnly(2026, 9, 2), stock: 120, reorder: 50, max: 200), // recovered
        };

        // Act
        var result = rule.Evaluate(history, leadTimeFactor: 1.0, alreadyOrderedQuantity: 0);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Evaluate_EmptyHistory_ReturnsNull()
    {
        // Arrange
        var rule = new ConsecutiveDaysReplenishmentRule(requiredConsecutiveDays: 2);

        // Act
        var result = rule.Evaluate([], leadTimeFactor: 1.0, alreadyOrderedQuantity: 0);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void Constructor_ValidDayCounts_DoesNotThrow(int days)
    {
        // Act & Assert
        var rule = new ConsecutiveDaysReplenishmentRule(days);
        Assert.NotNull(rule);
    }

    [Fact]
    public void Constructor_ZeroDayCount_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ConsecutiveDaysReplenishmentRule(requiredConsecutiveDays: 0));
    }

    [Fact]
    public void Evaluate_LeadTimeFactorScalesQuantity()
    {
        // Arrange — a 1.5x lead-time factor should round the quantity up.
        var rule = new ConsecutiveDaysReplenishmentRule(requiredConsecutiveDays: 1);
        var history = new List<StockSnapshot>
        {
            Snapshot("SKU-1003", new DateOnly(2026, 9, 1), stock: 50, reorder: 60, max: 100),
        };

        // Act
        var result = rule.Evaluate(history, leadTimeFactor: 1.5, alreadyOrderedQuantity: 0);

        // Assert — (100 - 50) * 1.5 = 75
        Assert.NotNull(result);
        Assert.Equal(75, result.SuggestedQuantity);
    }
}
