using InventoryControl.Domain.Shared;

namespace InventoryControl.Infrastructure.Events;

public interface IDomainEventDispatcher
{
    Task DispatchAsync(IDomainEvent @event, CancellationToken ct = default);
}
