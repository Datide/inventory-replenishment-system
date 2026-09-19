using System.ComponentModel.DataAnnotations;
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
public class CreateModel : PageModel
{
    private readonly ReplenishmentDbContext _db;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public CreateModel(ReplenishmentDbContext db, IStringLocalizer<SharedResource> localizer)
    {
        _db = db;
        _localizer = localizer;
    }

    [BindProperty]
    [Required, MaxLength(32)]
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

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        bool exists = await _db.Skus.AnyAsync(s => s.Code == Code);
        if (exists)
        {
            ModelState.AddModelError(nameof(Code), _localizer["Code '{0}' already exists.", Code]);
            return Page();
        }

        _db.Skus.Add(new Sku
        {
            Code = Code,
            Description = Description,
            UnitPrice = UnitPrice,
            ReorderPoint = ReorderPoint,
            MaxStock = MaxStock,
        });
        await _db.SaveChangesAsync();

        return RedirectToPage("Index");
    }
}
