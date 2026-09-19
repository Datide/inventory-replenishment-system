namespace Datide.Replenishment.Domain.Entities;

/// <summary>
/// An inventory item tracked by the system. Codes are business-unique
/// (e.g. "SKU-1001"). Soft-deactivation (IsActive) preserves history.
/// </summary>
public sealed class Sku
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>When current stock falls to this level, replenishment is considered.</summary>
    public int ReorderPoint { get; set; }

    /// <summary>Target ceiling for replenishment suggestions.</summary>
    public int MaxStock { get; set; }

    /// <summary>Per-unit cost used when estimating PO totals.</summary>
    public decimal UnitPrice { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
