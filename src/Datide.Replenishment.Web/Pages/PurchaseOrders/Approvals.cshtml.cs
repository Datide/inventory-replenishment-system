using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Datide.Replenishment.Domain.Entities;
using Datide.Replenishment.Infrastructure.Persistence;
using Datide.Replenishment.Web.Resources;

namespace Datide.Replenishment.Web.Pages.PurchaseOrders;

/// <summary>
/// Manager-only page: final approval gate for pending purchase orders.
/// Access is enforced by [Authorize(Roles = "Manager")].
///
/// As a last line of defence, approval is blocked when approving the PO would
/// push the SKU over its configured stock limit (MaxStock):
///   current stock + all in-flight quantity &gt; MaxStock → cannot approve.
/// </summary>
[Authorize(Roles = "Manager")]
public class ApprovalsModel : PageModel
{
    private readonly ReplenishmentDbContext _db;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public ApprovalsModel(ReplenishmentDbContext db, IStringLocalizer<SharedResource> localizer)
    {
        _db = db;
        _localizer = localizer;
    }

    public List<ApprovalItem> PendingOrders { get; private set; } = [];
    [TempData]
    public string? Message { get; set; }

    public async Task OnGetAsync()
    {
        PendingOrders = await LoadPendingAsync();
    }

    public async Task<IActionResult> OnPostApproveAsync(Guid id)
    {
        var user = await CurrentUserAsync();
        if (user is null) return Challenge();

        var po = await _db.PurchaseOrders.FindAsync(id);
        if (po is null) return NotFound();

        // Guard: only a pending order can be approved (prevents double decision).
        if (po.Status != PurchaseOrderStatus.Pending)
        {
            Message = _localizer["⚠️ PO {0} is no longer pending — no change made.", po.Id.ToString("N")[..8]];
            return RedirectToPage();
        }

        // Stock-limit guard: approving must not push the SKU over MaxStock.
        var limitCheck = await CheckStockLimitAsync(po.SkuCode);
        if (limitCheck.IsOverLimit)
        {
            Message = _localizer[
                "❌ PO {0} NOT approved — it would push {1} over its stock limit. " +
                "Current stock {2} + in-flight {3} = {4} exceeds limit {5} by {6}. Reject or ask staff to revise.",
                po.Id.ToString("N")[..8],
                po.SkuCode,
                limitCheck.CurrentStock,
                limitCheck.InFlight,
                limitCheck.ProjectedTotal,
                limitCheck.MaxStock,
                limitCheck.OverBy];
            return RedirectToPage();
        }

        po.Status = PurchaseOrderStatus.Approved;
        po.DecidedByUserId = user.Id;
        po.DecidedByUsername = user.Username;
        po.DecidedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        Message = _localizer["✅ PO {0} approved ({1} × {2}).", po.Id.ToString("N")[..8], po.Quantity, po.SkuCode];
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRejectAsync(Guid id, string reason)
    {
        var user = await CurrentUserAsync();
        if (user is null) return Challenge();

        var po = await _db.PurchaseOrders.FindAsync(id);
        if (po is null) return NotFound();

        if (po.Status != PurchaseOrderStatus.Pending)
        {
            Message = _localizer["⚠️ PO {0} is no longer pending — no change made.", po.Id.ToString("N")[..8]];
            return RedirectToPage();
        }

        po.Status = PurchaseOrderStatus.Rejected;
        po.DecidedByUserId = user.Id;
        po.DecidedByUsername = user.Username;
        po.DecidedAt = DateTime.UtcNow;
        po.RejectReason = string.IsNullOrWhiteSpace(reason)
            ? _localizer["Rejected by manager"]
            : reason.Trim();
        await _db.SaveChangesAsync();

        Message = _localizer["❌ PO {0} rejected.", po.Id.ToString("N")[..8]];
        return RedirectToPage();
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private async Task<List<ApprovalItem>> LoadPendingAsync()
    {
        var pending = await _db.PurchaseOrders.AsNoTracking()
            .Where(p => p.Status == PurchaseOrderStatus.Pending)
            .OrderBy(p => p.RequestedAt)
            .ToListAsync();

        var items = new List<ApprovalItem>();
        foreach (var po in pending)
        {
            items.Add(new ApprovalItem(po, await CheckStockLimitAsync(po.SkuCode)));
        }
        return items;
    }

    /// <summary>
    /// Computes projected stock for a SKU: latest snapshot current stock plus
    /// all in-flight quantity (draft + pending + approved POs).
    /// </summary>
    private async Task<StockLimitInfo> CheckStockLimitAsync(string skuCode)
    {
        var sku = await _db.Skus.AsNoTracking().FirstOrDefaultAsync(s => s.Code == skuCode);
        int maxStock = sku?.MaxStock ?? 0;

        var latest = await _db.StockSnapshots.AsNoTracking()
            .Where(s => s.Sku == skuCode)
            .OrderByDescending(s => s.SnapshotDate)
            .FirstOrDefaultAsync();
        int currentStock = latest?.CurrentStock ?? 0;

        int inFlight = await _db.PurchaseOrders.AsNoTracking()
            .Where(p => p.SkuCode == skuCode
                        && (p.Status == PurchaseOrderStatus.Draft
                            || p.Status == PurchaseOrderStatus.Pending
                            || p.Status == PurchaseOrderStatus.Approved))
            .SumAsync(p => (int?)p.Quantity) ?? 0;

        int projected = currentStock + inFlight;
        bool over = projected > maxStock;

        return new StockLimitInfo(
            maxStock,
            currentStock,
            inFlight,
            projected,
            over,
            Math.Max(projected - maxStock, 0));
    }

    private async Task<User?> CurrentUserAsync()
    {
        var name = User.Identity?.Name;
        if (string.IsNullOrEmpty(name)) return null;
        return await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == name);
    }

    // ------------------------------------------------------------------
    // View models
    // ------------------------------------------------------------------

    public sealed record StockLimitInfo(
        int MaxStock,
        int CurrentStock,
        int InFlight,
        int ProjectedTotal,
        bool IsOverLimit,
        int OverBy);

    public sealed record ApprovalItem(PurchaseOrder Po, StockLimitInfo Limit)
    {
        public PurchaseOrder Po { get; } = Po;
        public StockLimitInfo Limit { get; } = Limit;

        public bool IsBlockedByStockLimit => Limit.IsOverLimit;
    }
}
