using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Datide.Replenishment.Infrastructure.Persistence;

namespace Datide.Replenishment.Web.Pages.PurchaseOrders;

/// <summary>
/// Exports the current database contents to an .xlsx workbook — one sheet per
/// table. Useful for ad-hoc review, backups, or handing data to non-technical
/// stakeholders while the live source stays in SQLite.
/// </summary>
[Authorize]
public class ExportModel : PageModel
{
    private readonly ReplenishmentDbContext _db;

    public ExportModel(ReplenishmentDbContext db) => _db = db;

    public async Task<IActionResult> OnGetDownloadAsync()
    {
        var skus = await _db.Skus.AsNoTracking().OrderBy(s => s.Code).ToListAsync();
        var users = await _db.Users.AsNoTracking().OrderBy(u => u.Username).ToListAsync();
        var snapshots = await _db.StockSnapshots.AsNoTracking()
            .OrderBy(s => s.Sku).ThenBy(s => s.SnapshotDate).ToListAsync();
        var orders = await _db.PurchaseOrders.AsNoTracking()
            .OrderByDescending(p => p.RequestedAt).ToListAsync();

        using var workbook = new XLWorkbook();

        // --- SKUs ---
        var wsSku = workbook.Worksheets.Add("SKUs");
        wsSku.Cell(1, 1).SetValue("Id");
        wsSku.Cell(1, 2).SetValue("Code");
        wsSku.Cell(1, 3).SetValue("Description");
        wsSku.Cell(1, 4).SetValue("ReorderPoint");
        wsSku.Cell(1, 5).SetValue("MaxStock");
        wsSku.Cell(1, 6).SetValue("UnitPrice");
        wsSku.Cell(1, 7).SetValue("IsActive");
        wsSku.Cell(1, 8).SetValue("CreatedAt");
        int r = 2;
        foreach (var s in skus)
        {
            wsSku.Cell(r, 1).SetValue(s.Id);
            wsSku.Cell(r, 2).SetValue(s.Code);
            wsSku.Cell(r, 3).SetValue(s.Description);
            wsSku.Cell(r, 4).SetValue(s.ReorderPoint);
            wsSku.Cell(r, 5).SetValue(s.MaxStock);
            wsSku.Cell(r, 6).SetValue((double)s.UnitPrice);
            wsSku.Cell(r, 7).SetValue(s.IsActive);
            wsSku.Cell(r, 8).SetValue(s.CreatedAt.ToString("yyyy-MM-dd HH:mm"));
            r++;
        }
        wsSku.RangeUsed()?.SetAutoFilter();

        // --- Users ---
        var wsUser = workbook.Worksheets.Add("Users");
        wsUser.Cell(1, 1).SetValue("Id");
        wsUser.Cell(1, 2).SetValue("Username");
        wsUser.Cell(1, 3).SetValue("DisplayName");
        wsUser.Cell(1, 4).SetValue("Role");
        wsUser.Cell(1, 5).SetValue("IsActive");
        wsUser.Cell(1, 6).SetValue("CreatedAt");
        r = 2;
        foreach (var u in users)
        {
            wsUser.Cell(r, 1).SetValue(u.Id);
            wsUser.Cell(r, 2).SetValue(u.Username);
            wsUser.Cell(r, 3).SetValue(u.DisplayName);
            wsUser.Cell(r, 4).SetValue(u.Role.ToString());
            wsUser.Cell(r, 5).SetValue(u.IsActive);
            wsUser.Cell(r, 6).SetValue(u.CreatedAt.ToString("yyyy-MM-dd HH:mm"));
            r++;
        }
        wsUser.RangeUsed()?.SetAutoFilter();

        // --- Stock Snapshots ---
        var wsSnap = workbook.Worksheets.Add("StockSnapshots");
        wsSnap.Cell(1, 1).SetValue("Sku");
        wsSnap.Cell(1, 2).SetValue("SnapshotDate");
        wsSnap.Cell(1, 3).SetValue("CurrentStock");
        wsSnap.Cell(1, 4).SetValue("ReorderPoint");
        wsSnap.Cell(1, 5).SetValue("MaxStock");
        r = 2;
        foreach (var s in snapshots)
        {
            wsSnap.Cell(r, 1).SetValue(s.Sku);
            wsSnap.Cell(r, 2).SetValue(s.SnapshotDate.ToString("yyyy-MM-dd"));
            wsSnap.Cell(r, 3).SetValue(s.CurrentStock);
            wsSnap.Cell(r, 4).SetValue(s.ReorderPoint);
            wsSnap.Cell(r, 5).SetValue(s.MaxStock);
            r++;
        }
        wsSnap.RangeUsed()?.SetAutoFilter();

        // --- Purchase Orders ---
        var wsPo = workbook.Worksheets.Add("PurchaseOrders");
        wsPo.Cell(1, 1).SetValue("Id");
        wsPo.Cell(1, 2).SetValue("SkuCode");
        wsPo.Cell(1, 3).SetValue("Quantity");
        wsPo.Cell(1, 4).SetValue("UnitPrice");
        wsPo.Cell(1, 5).SetValue("Total");
        wsPo.Cell(1, 6).SetValue("Status");
        wsPo.Cell(1, 7).SetValue("RequestedBy");
        wsPo.Cell(1, 8).SetValue("RequestedAt");
        wsPo.Cell(1, 9).SetValue("DecidedBy");
        wsPo.Cell(1, 10).SetValue("DecidedAt");
        wsPo.Cell(1, 11).SetValue("RejectReason");
        r = 2;
        foreach (var p in orders)
        {
            wsPo.Cell(r, 1).SetValue(p.Id.ToString());
            wsPo.Cell(r, 2).SetValue(p.SkuCode);
            wsPo.Cell(r, 3).SetValue(p.Quantity);
            wsPo.Cell(r, 4).SetValue((double)p.UnitPrice);
            wsPo.Cell(r, 5).SetValue((double)p.Total);
            wsPo.Cell(r, 6).SetValue(p.Status.ToString());
            wsPo.Cell(r, 7).SetValue(p.RequestedByUsername);
            wsPo.Cell(r, 8).SetValue(p.RequestedAt.ToString("yyyy-MM-dd HH:mm"));
            wsPo.Cell(r, 9).SetValue(p.DecidedByUsername ?? "");
            wsPo.Cell(r, 10).SetValue(p.DecidedAt?.ToString("yyyy-MM-dd HH:mm") ?? "");
            wsPo.Cell(r, 11).SetValue(p.RejectReason ?? "");
            r++;
        }
        wsPo.RangeUsed()?.SetAutoFilter();

        // --- Formatting ---
        foreach (var ws in workbook.Worksheets)
        {
            ws.Columns().AdjustToContents();
            ws.SheetView.FreezeRows(1);
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        return File(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"datide_export_{DateTime.UtcNow:yyyyMMdd_HHmm}.xlsx");
    }
}
