namespace Datide.Replenishment.Application.Services;

using Datide.Replenishment.Application.Abstractions;
using Datide.Replenishment.Application.Contracts;
using Datide.Replenishment.Domain.Interfaces;

/// <summary>
/// Orchestrates a replenishment evaluation: reads stock history and in-flight
/// purchase-order quantities through repository abstractions, runs the
/// configured rule (which subtracts in-flight stock to avoid double-ordering),
/// and applies the manager-approval policy for high-value orders.
/// </summary>
public sealed class ReplenishmentService
{
    private readonly IStockSnapshotRepository _stockRepository;
    private readonly IPurchaseOrderRepository _poRepository;
    private readonly IReplenishmentRule _rule;

    public ReplenishmentService(
        IStockSnapshotRepository stockRepository,
        IPurchaseOrderRepository poRepository,
        IReplenishmentRule rule)
    {
        _stockRepository = stockRepository ?? throw new ArgumentNullException(nameof(stockRepository));
        _poRepository = poRepository ?? throw new ArgumentNullException(nameof(poRepository));
        _rule = rule ?? throw new ArgumentNullException(nameof(rule));
    }

    /// <summary>
    /// Evaluates a SKU and returns an <see cref="EvaluationResult"/>. Returns
    /// null when no replenishment is warranted (including when in-flight POs
    /// already cover the need).
    /// </summary>
    public async Task<EvaluationResult?> EvaluateAsync(
        EvaluateSkuRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        IReadOnlyList<Domain.Entities.StockSnapshot> history = await _stockRepository
            .GetRecentAsync(request.Sku, request.LookbackDays, cancellationToken);

        int inFlight = await _poRepository
            .GetInFlightQuantityAsync(request.Sku, cancellationToken);

        Domain.Entities.ReplenishmentSuggestion? suggestion = _rule.Evaluate(
            history, request.LeadTimeFactor, inFlight);

        if (suggestion is null)
        {
            return null;
        }

        bool requiresApproval = EstimateOrderCost(suggestion.SuggestedQuantity)
            > request.ManagerApprovalThreshold;

        return new EvaluationResult(
            suggestion.Sku,
            suggestion.SuggestedQuantity,
            suggestion.CurrentStock,
            suggestion.ReorderPoint,
            suggestion.ConsecutiveDays,
            suggestion.GrossNeed,
            suggestion.AlreadyOnOrder,
            requiresApproval);
    }

    // Demonstrates how a unit-price lookup would plug in; kept as a simple
    // estimate here so the sample stays self-contained.
    private static decimal EstimateOrderCost(int quantity) => quantity * 25m;
}
