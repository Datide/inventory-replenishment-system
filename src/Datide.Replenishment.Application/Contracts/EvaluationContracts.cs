namespace Datide.Replenishment.Application.Contracts;

/// <summary>
/// Result of a replenishment evaluation request. Mirrors the domain outcome
/// (structured facts, no pre-localised sentence) and adds the order-approval
/// decision that lives at the application level.
/// </summary>
public sealed record EvaluationResult(
    string Sku,
    int SuggestedQuantity,
    int CurrentStock,
    int ReorderPoint,
    int ConsecutiveDays,
    int GrossNeed,
    int AlreadyOnOrder,
    bool RequiresManagerApproval);

/// <summary>
/// Request to evaluate a single SKU for replenishment.
/// </summary>
public sealed record EvaluateSkuRequest(
    string Sku,
    int LookbackDays,
    double LeadTimeFactor = 1.0,
    decimal ManagerApprovalThreshold = 50_000m);
