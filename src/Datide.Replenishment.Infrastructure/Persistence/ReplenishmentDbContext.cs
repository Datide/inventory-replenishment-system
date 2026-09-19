namespace Datide.Replenishment.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Datide.Replenishment.Domain.Entities;

/// <summary>
/// EF Core DbContext for the Datide demo. Uses SQLite so the app runs with
/// zero setup — swap to SQL Server by changing the provider + connection string.
/// </summary>
public sealed class ReplenishmentDbContext : DbContext
{
    public ReplenishmentDbContext(DbContextOptions<ReplenishmentDbContext> options)
        : base(options)
    {
    }

    public DbSet<Sku> Skus => Set<Sku>();
    public DbSet<User> Users => Set<User>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<StockSnapshot> StockSnapshots => Set<StockSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // --- Sku ---
        modelBuilder.Entity<Sku>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.HasIndex(s => s.Code).IsUnique();
            entity.Property(s => s.Code).HasMaxLength(32).IsRequired();
            entity.Property(s => s.Description).HasMaxLength(200);
            entity.Property(s => s.UnitPrice).HasPrecision(18, 2);
        });

        // --- User ---
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.HasIndex(u => u.Username).IsUnique();
            entity.Property(u => u.Username).HasMaxLength(64).IsRequired();
            entity.Property(u => u.PasswordHash).HasMaxLength(512).IsRequired();
            entity.Property(u => u.DisplayName).HasMaxLength(100).IsRequired();
        });

        // --- PurchaseOrder ---
        modelBuilder.Entity<PurchaseOrder>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.SkuCode).HasMaxLength(32).IsRequired();
            entity.Property(p => p.RequestedByUsername).HasMaxLength(64);
            entity.Property(p => p.DecidedByUsername).HasMaxLength(64);
            entity.Property(p => p.RejectReason).HasMaxLength(500);
            entity.Property(p => p.UnitPrice).HasPrecision(18, 2);
            entity.HasIndex(p => new { p.SkuCode, p.Status });
        });

        // --- StockSnapshot (daily history per SKU) ---
        modelBuilder.Entity<StockSnapshot>(entity =>
        {
            entity.HasKey(s => new { s.Sku, s.SnapshotDate });
            entity.Property(s => s.Sku).HasMaxLength(32).IsRequired();
        });
    }
}
