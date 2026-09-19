namespace Datide.Replenishment.Infrastructure.Persistence;

using Microsoft.AspNetCore.Identity;
using Datide.Replenishment.Domain.Entities;

/// <summary>
/// Seeds the SQLite database on first run: two users (manager + staff),
/// a realistic set of SKUs, and a few days of stock snapshots so the
/// dashboard has meaningful content immediately.
/// </summary>
public static class DatabaseSeeder
{
    public static void Seed(ReplenishmentDbContext db)
    {
        if (db.Skus.Any() || db.Users.Any())
        {
            return; // already seeded
        }

        var hasher = new PasswordHasher<User>();

        // --- Users ---
        var manager = new User
        {
            Username = "manager",
            DisplayName = "Alex Manager",
            Role = UserRole.Manager,
            IsActive = true,
        };
        manager.PasswordHash = hasher.HashPassword(manager, "Manager@123");

        var staff = new User
        {
            Username = "staff",
            DisplayName = "Sam Staff",
            Role = UserRole.Staff,
            IsActive = true,
        };
        staff.PasswordHash = hasher.HashPassword(staff, "Staff@123");

        db.Users.AddRange(manager, staff);

        // --- SKUs ---
        var skus = new[]
        {
            new Sku { Code = "SKU-1001", Description = "Industrial bearings, 40mm", ReorderPoint = 50, MaxStock = 200, UnitPrice = 25m },
            new Sku { Code = "SKU-1002", Description = "Drive belts, type B", ReorderPoint = 60, MaxStock = 250, UnitPrice = 18m },
            new Sku { Code = "SKU-1003", Description = "Lubricant, 5L can", ReorderPoint = 40, MaxStock = 200, UnitPrice = 32m },
            new Sku { Code = "SKU-1004", Description = "Hydraulic pumps, 7.5kW", ReorderPoint = 100, MaxStock = 3000, UnitPrice = 125m },
            new Sku { Code = "SKU-1005", Description = "Fasteners M8, box of 500", ReorderPoint = 200, MaxStock = 1500, UnitPrice = 8m },
        };
        db.Skus.AddRange(skus);

        // --- Stock snapshots (last 3 days) ---
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var snapshots = new List<StockSnapshot>
        {
            // SKU-1001 low for 3 days → will trigger
            Sn("SKU-1001", today.AddDays(-2), 8), Sn("SKU-1001", today.AddDays(-1), 8), Sn("SKU-1001", today, 8),
            // SKU-1002 dipped then recovered → dampened
            Sn("SKU-1002", today.AddDays(-2), 12), Sn("SKU-1002", today.AddDays(-1), 130), Sn("SKU-1002", today, 120),
            // SKU-1003 healthy
            Sn("SKU-1003", today.AddDays(-2), 170), Sn("SKU-1003", today.AddDays(-1), 175), Sn("SKU-1003", today, 180),
            // SKU-1004 low, huge max → high value order needing approval
            Sn("SKU-1004", today.AddDays(-2), 20), Sn("SKU-1004", today.AddDays(-1), 20), Sn("SKU-1004", today, 20),
            // SKU-1005 moderate
            Sn("SKU-1005", today.AddDays(-2), 240), Sn("SKU-1005", today.AddDays(-1), 230), Sn("SKU-1005", today, 220),
        };
        db.StockSnapshots.AddRange(snapshots);

        db.SaveChanges();
    }

    private static StockSnapshot Sn(string sku, DateOnly date, int stock)
    {
        // Pull reorder/max from the seeded SKU list for consistency.
        return sku switch
        {
            "SKU-1001" => new StockSnapshot(sku, stock, 50, 200, date),
            "SKU-1002" => new StockSnapshot(sku, stock, 60, 250, date),
            "SKU-1003" => new StockSnapshot(sku, stock, 40, 200, date),
            "SKU-1004" => new StockSnapshot(sku, stock, 100, 3000, date),
            "SKU-1005" => new StockSnapshot(sku, stock, 200, 1500, date),
            _ => new StockSnapshot(sku, stock, 0, 0, date),
        };
    }
}
