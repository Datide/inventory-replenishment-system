using System.ComponentModel.DataAnnotations;
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
/// Edit page for a DRAFT purchase order. Staff can adjust the quantity before
/// submitting for approval. Only draft POs are editable.
/// </summary>
[Authorize]
public class EditModel : PageModel
{
    private readonly ReplenishmentDbContext _db;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public EditModel(ReplenishmentDbContext db, IStringLocalizer<SharedResource> localizer)
    {
        _db = db;
        _localizer = localizer;
    }

    public Guid Id { get; set; }

    public PurchaseOrder? Po { get; set; }

    [BindProperty]
    [Range(1, 10_000_000)]
    public int Quantity { get; set; }

    public string? ErrorMessage { get; set; }

    [TempData]
    public string? Message { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        Id = id;
        var po = await _db.PurchaseOrders.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (po is null) return NotFound();

        if (po.Status != PurchaseOrderStatus.Draft)
        {
            ErrorMessage = _localizer["Only draft POs can be edited."];
        }
        else
        {
            Quantity = po.Quantity;
        }

        Po = po;
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(Guid id)
    {
        Id = id;
        var po = await _db.PurchaseOrders.FindAsync(id);
        if (po is null) return NotFound();

        if (po.Status != PurchaseOrderStatus.Draft)
        {
            ErrorMessage = _localizer["This PO is no longer a draft and cannot be edited."];
            Po = po;
            return Page();
        }

        if (!ModelState.IsValid)
        {
            Po = po;
            return Page();
        }

        po.Quantity = Quantity;
        await _db.SaveChangesAsync();

        Message = _localizer["✅ PO {0} quantity updated to {1}.", po.Id.ToString("N")[..8], Quantity];
        return RedirectToPage("Index");
    }

    /// <summary>Draft → Pending: locks the PO for manager approval.</summary>
    public async Task<IActionResult> OnPostSubmitAsync(Guid id)
    {
        var po = await _db.PurchaseOrders.FindAsync(id);
        if (po is null) return NotFound();

        if (po.Status != PurchaseOrderStatus.Draft)
        {
            ErrorMessage = _localizer["This PO is no longer a draft."];
            Po = po;
            return Page();
        }

        po.Status = PurchaseOrderStatus.Pending;
        po.SubmittedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        Message = _localizer["📤 PO {0} submitted for approval.", po.Id.ToString("N")[..8]];
        return RedirectToPage("Index");
    }
}
