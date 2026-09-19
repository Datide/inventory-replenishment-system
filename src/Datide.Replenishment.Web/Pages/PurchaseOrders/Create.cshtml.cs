using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Datide.Replenishment.Application.Contracts;
using Datide.Replenishment.Application.Services;
using Datide.Replenishment.Domain.Entities;
using Datide.Replenishment.Infrastructure.Persistence;
using Datide.Replenishment.Web.Resources;

namespace Datide.Replenishment.Web.Pages.PurchaseOrders;

[Authorize]
public class CreateModel : PageModel
{
    private readonly ReplenishmentDbContext _db;
    private readonly ReplenishmentService _replenishment;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public CreateModel(
        ReplenishmentDbContext db,
        ReplenishmentService replenishment,
        IStringLocalizer<SharedResource> localizer)
    {
        _db = db;
        _replenishment = replenishment;
        _localizer = localizer;
    }

    [BindProperty]
    public string SkuCode { get; set; } = string.Empty;

    [BindProperty]
    public int Quantity { get; set; } = 1;

    public List<SelectListItem> SkuOptions { get; private set; } = [];
    public string SkuJson { get; private set; } = "{}";
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        await LoadOptionsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await CurrentUserAsync();
        if (user is null) return Challenge();
        await LoadOptionsAsync();

        var sku = await _db.Skus.AsNoTracking().FirstOrDefaultAsync(s => s.Code == SkuCode && s.IsActive);
        if (sku is null)
        {
            ErrorMessage = _localizer["Selected SKU not found or inactive."];
            return Page();
        }

        if (Quantity <= 0)
        {
            ErrorMessage = _localizer["Quantity must be greater than zero."];
            return Page();
        }

        // Duplicate guard: block a second OPEN PO (draft or pending) for the same SKU.
        bool hasOpen = await _db.PurchaseOrders
            .AnyAsync(p => p.SkuCode == SkuCode
                        && (p.Status == PurchaseOrderStatus.Draft
                            || p.Status == PurchaseOrderStatus.Pending));
        if (hasOpen)
        {
            ErrorMessage = _localizer["⚠️ {0} already has a draft/pending PO. Edit that PO instead of creating another.", SkuCode];
            return Page();
        }

        // Snapshot the system's current need for this SKU as the warning reference.
        int? required = await GetRequiredQuantityAsync(sku.Code);

        var po = new PurchaseOrder
        {
            SkuCode = sku.Code,
            Quantity = Quantity,
            UnitPrice = sku.UnitPrice,
            Status = PurchaseOrderStatus.Draft,
            RequiredQuantity = required,
            RequestedByUserId = user.Id,
            RequestedByUsername = user.Username,
        };
        _db.PurchaseOrders.Add(po);
        await _db.SaveChangesAsync();

        return RedirectToPage("Edit", new { id = po.Id });
    }

    /// <summary>
    /// Computes the current gross replenishment need for a SKU (from the domain
    /// rule) so the PO can carry a stable warning reference.
    /// </summary>
    private async Task<int?> GetRequiredQuantityAsync(string sku)
    {
        try
        {
            EvaluationResult? result = await _replenishment.EvaluateAsync(
                new EvaluateSkuRequest(sku, LookbackDays: 7));
            return result?.GrossNeed;
        }
        catch
        {
            return null; // no snapshot data / rule silent → no reference
        }
    }

    private async Task<User?> CurrentUserAsync()
    {
        var name = User.Identity?.Name;
        if (string.IsNullOrEmpty(name)) return null;
        return await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == name);
    }

    private async Task LoadOptionsAsync()
    {
        var activeSkus = await _db.Skus.AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Code)
            .ToListAsync();

        var openCounts = await _db.PurchaseOrders.AsNoTracking()
            .Where(p => p.Status == PurchaseOrderStatus.Draft
                     || p.Status == PurchaseOrderStatus.Pending)
            .GroupBy(p => p.SkuCode)
            .ToDictionaryAsync(g => g.Key, g => g.Count());

        var latestSnapshots = await _db.StockSnapshots.AsNoTracking()
            .GroupBy(s => s.Sku)
            .Select(g => g.OrderByDescending(x => x.SnapshotDate).First())
            .ToDictionaryAsync(x => x.Sku);

        SkuOptions = activeSkus
            .Select(s => new SelectListItem($"{s.Code} — {s.Description}", s.Code))
            .ToList();

        var data = new Dictionary<string, object>();
        foreach (var s in activeSkus)
        {
            var snap = latestSnapshots.GetValueOrDefault(s.Code);
            data[s.Code] = new
            {
                code = s.Code,
                description = s.Description,
                current = snap?.CurrentStock ?? 0,
                reorder = s.ReorderPoint,
                max = s.MaxStock,
                price = s.UnitPrice,
                openCount = openCounts.GetValueOrDefault(s.Code),
            };
        }
        SkuJson = JsonSerializer.Serialize(data);
    }
}
