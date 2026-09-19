using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Datide.Replenishment.Domain.Entities;
using Datide.Replenishment.Infrastructure.Persistence;
using Datide.Replenishment.Web.Resources;

namespace Datide.Replenishment.Web.Pages.PurchaseOrders;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ReplenishmentDbContext _db;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public IndexModel(ReplenishmentDbContext db, IStringLocalizer<SharedResource> localizer)
    {
        _db = db;
        _localizer = localizer;
    }

    public List<PurchaseOrder> Orders { get; private set; } = [];
    [TempData]
    public string? Message { get; set; }

    public async Task OnGetAsync()
    {
        Orders = await _db.PurchaseOrders.AsNoTracking()
            .OrderByDescending(p => p.RequestedAt)
            .Take(200)
            .ToListAsync();
    }

    /// <summary>Submit a draft PO for approval (Draft → Pending).</summary>
    public async Task<IActionResult> OnPostSubmitAsync(Guid id)
    {
        var po = await _db.PurchaseOrders.FindAsync(id);
        if (po is null) return NotFound();

        if (po.Status != PurchaseOrderStatus.Draft)
        {
            Message = _localizer["⚠️ PO {0} is not a draft.", po.Id.ToString("N")[..8]];
            return RedirectToPage();
        }

        po.Status = PurchaseOrderStatus.Pending;
        po.SubmittedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        Message = _localizer["📤 PO {0} submitted for approval.", po.Id.ToString("N")[..8]];
        return RedirectToPage();
    }
}
