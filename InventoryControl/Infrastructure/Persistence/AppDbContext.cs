using InventoryControl.Domain.Shared;
using InventoryControl.Infrastructure.Events;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace InventoryControl.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    private readonly IDomainEventDispatcher? _dispatcher;

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public AppDbContext(DbContextOptions<AppDbContext> options, IDomainEventDispatcher dispatcher) : base(options)
    {
        _dispatcher = dispatcher;
    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<ProcessedOrder> ProcessedOrders => Set<ProcessedOrder>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SyncState> SyncStates => Set<SyncState>();
    public DbSet<ProductExternalMapping> ProductExternalMappings => Set<ProductExternalMapping>();
    public DbSet<CategoryExternalMapping> CategoryExternalMappings => Set<CategoryExternalMapping>();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entitiesWithEvents = ChangeTracker.Entries<IHasDomainEvents>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        var result = await base.SaveChangesAsync(cancellationToken);

        if (_dispatcher is not null)
        {
            var events = entitiesWithEvents.SelectMany(e => e.DomainEvents).ToList();
            foreach (var entity in entitiesWithEvents)
                entity.ClearDomainEvents();

            foreach (var @event in events)
                await _dispatcher.DispatchAsync(@event, cancellationToken);
        }

        return result;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).IsRequired().HasMaxLength(100);
            entity.Property(c => c.Description).HasMaxLength(500);

            entity.HasOne(c => c.Parent)
                  .WithMany(c => c.Children)
                  .HasForeignKey(c => c.ParentId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Name).IsRequired().HasMaxLength(200);
            entity.Property(s => s.Cnpj).HasMaxLength(18);
            entity.Property(s => s.Phone).HasMaxLength(20);
            entity.Property(s => s.Email).HasMaxLength(100);
            entity.Property(s => s.ContactName).HasMaxLength(200);
            entity.Property(s => s.Notes).HasMaxLength(1000);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Name).IsRequired().HasMaxLength(200);
            entity.Property(p => p.Description).HasMaxLength(500);
            entity.Property(p => p.CostPrice).HasColumnType("decimal(10,2)");
            entity.Property(p => p.SellingPrice).HasColumnType("decimal(10,2)");
            entity.Property(p => p.Sku).HasMaxLength(100);
            entity.HasIndex(p => p.Sku)
                  .IsUnique()
                  .HasFilter("\"Sku\" IS NOT NULL");
            entity.Property(p => p.Brand).HasMaxLength(100);
            entity.HasIndex(p => p.Brand);
            entity.HasIndex(p => new { p.CurrentStock, p.MinimumStock });

            entity.HasOne(p => p.Category)
                  .WithMany(c => c.Products)
                  .HasForeignKey(p => p.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.Ignore(p => p.PrimaryImagePath);
        });

        modelBuilder.Entity<ProductImage>(entity =>
        {
            entity.HasKey(pi => pi.Id);
            entity.Property(pi => pi.ImagePath).IsRequired().HasMaxLength(500);
            entity.Property(pi => pi.AltText).HasMaxLength(200);

            entity.HasOne(pi => pi.Product)
                  .WithMany(p => p.Images)
                  .HasForeignKey(pi => pi.ProductId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StockMovement>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Type).IsRequired();
            entity.Property(m => m.UnitCost).HasColumnType("decimal(10,2)");
            entity.Property(m => m.Notes).HasMaxLength(500);
            entity.HasIndex(m => m.Date);

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

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.UserId).IsRequired().HasMaxLength(256);
            entity.Property(a => a.UserName).IsRequired().HasMaxLength(100);
            entity.Property(a => a.Action).IsRequired().HasMaxLength(50);
            entity.Property(a => a.EntityName).IsRequired().HasMaxLength(100);
            entity.Property(a => a.EntityId).HasMaxLength(100);
            entity.Property(a => a.OldValues).HasMaxLength(4000);
            entity.Property(a => a.NewValues).HasMaxLength(4000);
            entity.HasIndex(a => a.Timestamp);
        });

        modelBuilder.Entity<SyncState>(entity =>
        {
            entity.HasKey(s => s.Key);
            entity.Property(s => s.Key).HasMaxLength(100);
        });

        modelBuilder.Entity<ProductExternalMapping>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.StoreName).IsRequired().HasMaxLength(100);
            entity.Property(m => m.ExternalId).IsRequired().HasMaxLength(200);
            entity.Property(m => m.Platform).IsRequired().HasMaxLength(50);

            entity.HasIndex(m => new { m.ProductId, m.StoreName }).IsUnique();
            entity.HasIndex(m => new { m.StoreName, m.ExternalId });

            entity.HasOne(m => m.Product)
                  .WithMany(p => p.ExternalMappings)
                  .HasForeignKey(m => m.ProductId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CategoryExternalMapping>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.StoreName).IsRequired().HasMaxLength(100);
            entity.Property(m => m.ExternalId).IsRequired().HasMaxLength(200);
            entity.Property(m => m.Platform).IsRequired().HasMaxLength(50);

            entity.HasIndex(m => new { m.CategoryId, m.StoreName }).IsUnique();
            entity.HasIndex(m => new { m.StoreName, m.ExternalId });

            entity.HasOne(m => m.Category)
                  .WithMany(c => c.ExternalMappings)
                  .HasForeignKey(m => m.CategoryId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
