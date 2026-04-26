using InventoryControl.Domain.Shared;

namespace InventoryControl.Infrastructure.Events;

public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent @event, CancellationToken ct);
}
