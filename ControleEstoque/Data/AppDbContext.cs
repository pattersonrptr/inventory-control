using ControleEstoque.Models;
using Microsoft.EntityFrameworkCore;

namespace ControleEstoque.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<ProcessedOrder> ProcessedOrders => Set<ProcessedOrder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).IsRequired().HasMaxLength(100);
            entity.Property(c => c.Description).HasMaxLength(500);
            entity.Property(c => c.ExternalId).HasMaxLength(200);
            entity.Property(c => c.ExternalIdSource).HasMaxLength(50);
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Name).IsRequired().HasMaxLength(200);
            entity.Property(s => s.Cnpj).HasMaxLength(18);
            entity.Property(s => s.Phone).HasMaxLength(20);
            entity.Property(s => s.Email).HasMaxLength(100);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Name).IsRequired().HasMaxLength(200);
            entity.Property(p => p.Description).HasMaxLength(500);
            entity.Property(p => p.CostPrice).HasColumnType("decimal(10,2)");
            entity.Property(p => p.SellingPrice).HasColumnType("decimal(10,2)");
            entity.Property(p => p.Sku).HasMaxLength(100);
            entity.Property(p => p.ExternalId).HasMaxLength(200);
            entity.Property(p => p.ExternalIdSource).HasMaxLength(50);

            entity.HasOne(p => p.Category)
                  .WithMany(c => c.Products)
                  .HasForeignKey(p => p.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.Supplier)
                  .WithMany(s => s.Products)
                  .HasForeignKey(p => p.SupplierId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StockMovement>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Type).IsRequired();
            entity.Property(m => m.UnitCost).HasColumnType("decimal(10,2)");
            entity.Property(m => m.Notes).HasMaxLength(500);

            entity.HasOne(m => m.Product)
                  .WithMany(p => p.StockMovements)
                  .HasForeignKey(m => m.ProductId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(m => m.Supplier)
                  .WithMany()
                  .HasForeignKey(m => m.SupplierId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .IsRequired(false);
        });

        modelBuilder.Entity<ProcessedOrder>(entity =>
        {
            entity.HasKey(po => po.Id);
            entity.Property(po => po.ExternalOrderId).IsRequired().HasMaxLength(200);
            entity.Property(po => po.Status).IsRequired().HasMaxLength(50);
            entity.HasIndex(po => po.ExternalOrderId).IsUnique();
        });
    }
}
