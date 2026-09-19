using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Datide.Replenishment.Application.Contracts;
using Datide.Replenishment.Application.Services;
using Datide.Replenishment.Domain.Entities;
using Datide.Replenishment.Infrastructure.Persistence;
using Datide.Replenishment.Web.Resources;

namespace Datide.Replenishment.Web.Pages;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ReplenishmentDbContext _db;
    private readonly ReplenishmentService _service;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public IndexModel(
        ReplenishmentDbContext db,
        ReplenishmentService service,
        IStringLocalizer<SharedResource> localizer)
    {
        _db = db;
        _service = service;
        _localizer = localizer;
    }

    public int TotalSkus { get; private set; }
    public int LowCount { get; private set; }
    public int PendingApprovalCount { get; private set; }
    public List<SuggestionRow> Suggestions { get; private set; } = [];
    public List<StockRow> StockRows { get; private set; } = [];
    [TempData]
    public string? Message { get; set; }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostRunCheckAsync()
    {
        await LoadAsync();
        if (Suggestions.Count == 0)
        {
            Message = _localizer["No replenishment needed — all SKUs are healthy."];
        }
        else
        {
            Message = _localizer["Check complete — {0} SKU(s) need replenishment.", Suggestions.Count];
        }
        return Page();
    }

    public async Task<IActionResult> OnPostCreatePoAsync(string sku, int qty)
    {
        var user = await CurrentUserAsync();
        if (user is null) return Challenge();

        var skuEntity = await _db.Skus.AsNoTracking().FirstOrDefaultAsync(s => s.Code == sku);
        if (skuEntity is null) return NotFound();

        // Duplicate guard: no second OPEN PO (draft/pending) for the same SKU.
        bool hasOpen = await _db.PurchaseOrders
            .AnyAsync(p => p.SkuCode == sku
                        && (p.Status == PurchaseOrderStatus.Draft
                            || p.Status == PurchaseOrderStatus.Pending));
        if (hasOpen)
        {
            Message = _localizer["⚠️ {0} already has a draft/pending PO — edit that instead of creating another.", sku];
            await LoadAsync();
            return Page();
        }

        // Snapshot the current gross need as the warning reference for the draft.
        int? required = null;
        try
        {
            var result = await _service.EvaluateAsync(new EvaluateSkuRequest(sku, LookbackDays: 7));
            required = result?.GrossNeed;
        }
        catch
        {
            // no snapshot data → no reference
        }

        var po = new PurchaseOrder
        {
            SkuCode = sku,
            Quantity = qty,
            UnitPrice = skuEntity.UnitPrice,
            Status = PurchaseOrderStatus.Draft,
            RequiredQuantity = required,
            RequestedByUserId = user.Id,
            RequestedByUsername = user.Username,
        };
        _db.PurchaseOrders.Add(po);
        await _db.SaveChangesAsync();

        return RedirectToPage("/PurchaseOrders/Edit", new { id = po.Id });
    }

    // --- helpers ---

    private async Task<User?> CurrentUserAsync()
    {
        var name = User.Identity?.Name;
        if (string.IsNullOrEmpty(name)) return null;
        return await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == name);
    }

    private async Task LoadAsync()
    {
        var skus = await _db.Skus.AsNoTracking().ToListAsync();
        TotalSkus = skus.Count;

        // Latest snapshot per SKU (fall back to seeded current-stock concept).
        var latest = await _db.StockSnapshots.AsNoTracking()
            .GroupBy(s => s.Sku)
            .Select(g => g.OrderByDescending(x => x.SnapshotDate).First())
            .ToListAsync();

        var poCounts = await _db.PurchaseOrders.AsNoTracking()
            .GroupBy(p => p.SkuCode)
            .Select(g => new
            {
                Sku = g.Key,
                Pending = g.Count(p => p.Status == PurchaseOrderStatus.Pending),
                Total = g.Count(),
            })
            .ToListAsync();

        PendingApprovalCount = poCounts.Sum(p => p.Pending);

        StockRows = skus.Select(s =>
        {
            var snap = latest.FirstOrDefault(x => x.Sku == s.Code);
            var counts = poCounts.FirstOrDefault(x => x.Sku == s.Code);
            int current = snap?.CurrentStock ?? s.MaxStock / 2;
            bool low = current <= s.ReorderPoint;
            if (low) LowCount++;
            return new StockRow
            {
                Code = s.Code,
                Description = s.Description,
                CurrentStock = current,
                ReorderPoint = s.ReorderPoint,
                MaxStock = s.MaxStock,
                IsBelowReorder = low,
                PendingPoCount = counts?.Pending ?? 0,
                TotalPoCount = counts?.Total ?? 0,
            };
        }).ToList();

        // Replenishment suggestions via the domain rule (needs recent snapshots).
        Suggestions = [];
        foreach (var sku in skus)
        {
            var result = await _service.EvaluateAsync(new EvaluateSkuRequest(sku.Code, LookbackDays: 7));
            if (result is not null)
            {
                var counts = poCounts.FirstOrDefault(x => x.Sku == sku.Code);
                Suggestions.Add(new SuggestionRow
                {
                    Sku = result.Sku,
                    Quantity = result.SuggestedQuantity,
                    RequiresApproval = result.RequiresManagerApproval,
                    GrossNeed = result.GrossNeed,
                    AlreadyOnOrder = result.AlreadyOnOrder,
                    CurrentStock = result.CurrentStock,
                    ReorderPoint = result.ReorderPoint,
                    ConsecutiveDays = result.ConsecutiveDays,
                    HasPendingPo = counts?.Pending > 0,
                });
            }
        }
    }

    public sealed class StockRow
    {
        public string Code { get; init; } = "";
        public string Description { get; init; } = "";
        public int CurrentStock { get; init; }
        public int ReorderPoint { get; init; }
        public int MaxStock { get; init; }
        public bool IsBelowReorder { get; init; }
        public int PendingPoCount { get; init; }
        public int TotalPoCount { get; init; }
    }

    public sealed class SuggestionRow
    {
        public string Sku { get; init; } = "";
        public int Quantity { get; init; }
        public bool RequiresApproval { get; init; }
        public int GrossNeed { get; init; }
        public int AlreadyOnOrder { get; init; }
        public int CurrentStock { get; init; }
        public int ReorderPoint { get; init; }
        public int ConsecutiveDays { get; init; }
        public bool HasPendingPo { get; init; }
    }
}
