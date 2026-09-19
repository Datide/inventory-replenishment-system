namespace Datide.Replenishment.Domain.Entities;

public enum PurchaseOrderStatus
{
    /// <summary>
    /// Created by staff but not yet submitted — still editable.
    /// </summary>
    Draft = 0,

    /// <summary>Submitted by staff — awaiting manager approval (no longer editable).</summary>
    Pending = 1,

    /// <summary>Approved by a manager — order is final.</summary>
    Approved = 2,

    /// <summary>Rejected by a manager.</summary>
    Rejected = 3,
}

/// <summary>
/// A purchase order created from an approved replenishment suggestion.
/// Uses a GUID as the order number so duplicate numbers are impossible even
/// under concurrent creation. Audit fields record exactly who did what.
/// </summary>
public sealed class PurchaseOrder
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string SkuCode { get; set; } = string.Empty;

    public int Quantity { get; set; }

    /// <summary>Unit price snapshot at creation time (protects against later price changes).</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// The system's computed need (gross target) for this SKU at the moment the
    /// PO was created. Used as the reference for over/under-order warnings:
    /// quantity &gt; required → yellow (over-order); quantity &lt; required → staff confirm.
    /// Null when the PO was not created against an active replenishment need.
    /// </summary>
    public int? RequiredQuantity { get; set; }

    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;

    // --- Creation audit ---
    public int RequestedByUserId { get; set; }
    public string RequestedByUsername { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    // --- Submission audit (Draft → Pending) ---
    public DateTime? SubmittedAt { get; set; }

    // --- Decision audit ---
    public int? DecidedByUserId { get; set; }
    public string? DecidedByUsername { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? RejectReason { get; set; }

    /// <summary>Total value = quantity × unit price.</summary>
    public decimal Total => Quantity * UnitPrice;

    /// <summary>True when quantity exceeds the system's recommended amount.</summary>
    public bool IsOverRequired => RequiredQuantity.HasValue && Quantity > RequiredQuantity.Value;

    /// <summary>True when quantity is below the system's recommended amount.</summary>
    public bool IsUnderRequired => RequiredQuantity.HasValue && Quantity < RequiredQuantity.Value;
}
