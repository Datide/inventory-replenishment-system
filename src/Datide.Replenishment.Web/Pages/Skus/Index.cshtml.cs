using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Datide.Replenishment.Domain.Entities;
using Datide.Replenishment.Infrastructure.Persistence;
using Datide.Replenishment.Web.Resources;

namespace Datide.Replenishment.Web.Pages.Skus;

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

    public List<Sku> Skus { get; private set; } = [];
    [TempData]
    public string? Message { get; set; }

    public async Task OnGetAsync()
    {
        Skus = await _db.Skus.AsNoTracking()
            .OrderBy(s => s.Code)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostToggleAsync(int id)
    {
        var sku = await _db.Skus.FindAsync(id);
        if (sku is null) return NotFound();

        sku.IsActive = !sku.IsActive;
        sku.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        Message = sku.IsActive
            ? _localizer["✅ {0} reactivated.", sku.Code]
            : _localizer["⏸ {0} deactivated (history preserved).", sku.Code];
        return RedirectToPage();
    }
}
