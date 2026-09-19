using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Datide.Replenishment.Infrastructure.Persistence;

namespace Datide.Replenishment.Web.Pages.Skus;

[Authorize]
public class EditModel : PageModel
{
    private readonly ReplenishmentDbContext _db;

    public EditModel(ReplenishmentDbContext db) => _db = db;

    [BindProperty]
    public int Id { get; set; }

    [BindProperty]
    [Display(Name = "Code")]
    public string Code { get; set; } = string.Empty;

    [BindProperty]
    [Required, MaxLength(200)]
    [Display(Name = "Description")]
    public string Description { get; set; } = string.Empty;

    [BindProperty]
    [Range(0.01, 1_000_000)]
    [Display(Name = "Unit price (USD)")]
    public decimal UnitPrice { get; set; }

    [BindProperty]
    [Range(0, 1_000_000)]
    [Display(Name = "Reorder point")]
    public int ReorderPoint { get; set; }

    [BindProperty]
    [Range(1, 10_000_000)]
    [Display(Name = "Max stock")]
    public int MaxStock { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var sku = await _db.Skus.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        if (sku is null) return NotFound();

        Id = sku.Id;
        Code = sku.Code;
        Description = sku.Description;
        UnitPrice = sku.UnitPrice;
        ReorderPoint = sku.ReorderPoint;
        MaxStock = sku.MaxStock;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var sku = await _db.Skus.FindAsync(Id);
        if (sku is null) return NotFound();

        sku.Description = Description;
        sku.UnitPrice = UnitPrice;
        sku.ReorderPoint = ReorderPoint;
        sku.MaxStock = MaxStock;
        sku.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
