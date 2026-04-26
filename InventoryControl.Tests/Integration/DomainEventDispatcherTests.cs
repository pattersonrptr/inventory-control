using InventoryControl.Domain.Shared;
using InventoryControl.Infrastructure.Events;
using InventoryControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace InventoryControl.Tests.Integration;

public class DomainEventDispatcherTests
{
    [Fact]
    public async Task SaveChangesAsync_ProductRaisedEvents_HandlerIsInvoked()
    {
        var captured = new List<IDomainEvent>();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o =>
            o.UseInMemoryDatabase("EventsTest_" + Guid.NewGuid())
             .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<IDomainEventHandler<StockChanged>>(_ => new CapturingHandler<StockChanged>(captured));
        services.AddScoped<IDomainEventHandler<ProductWentBelowMinimum>>(_ => new CapturingHandler<ProductWentBelowMinimum>(captured));

        var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var category = new Category { Name = "Test" };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var product = new Product
        {
            Name = "Widget",
            CostPrice = 1m,
            SellingPrice = 2m,
            CategoryId = category.Id,
            CurrentStock = 5,
            MinimumStock = 3
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        // Trigger threshold crossing
        product.ApplyExit(3);
        await db.SaveChangesAsync();

        Assert.Contains(captured, e => e is StockChanged sc && sc.NewStock == 2);
        Assert.Contains(captured, e => e is ProductWentBelowMinimum p && p.CurrentStock == 2);
    }

    [Fact]
    public async Task SaveChangesAsync_ClearsDomainEventsAfterDispatch()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o =>
            o.UseInMemoryDatabase("EventsTest_" + Guid.NewGuid())
             .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

        var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var category = new Category { Name = "Test" };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var product = new Product
        {
            Name = "Widget",
            CostPrice = 1m,
            SellingPrice = 2m,
            CategoryId = category.Id,
            CurrentStock = 10,
            MinimumStock = 1
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        product.ApplyEntry(5);
        Assert.NotEmpty(product.DomainEvents);
        await db.SaveChangesAsync();
        Assert.Empty(product.DomainEvents);
    }

    private class CapturingHandler<T> : IDomainEventHandler<T> where T : IDomainEvent
    {
        private readonly List<IDomainEvent> _captured;
        public CapturingHandler(List<IDomainEvent> captured) => _captured = captured;
        public Task HandleAsync(T @event, CancellationToken ct)
        {
            _captured.Add(@event);
            return Task.CompletedTask;
        }
    }
}
