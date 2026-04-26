using InventoryControl.Domain.Shared;

namespace InventoryControl.Infrastructure.Events;

public class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DomainEventDispatcher> _logger;

    public DomainEventDispatcher(IServiceProvider serviceProvider, ILogger<DomainEventDispatcher> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task DispatchAsync(IDomainEvent @event, CancellationToken ct = default)
    {
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(@event.GetType());
        var handlers = _serviceProvider.GetServices(handlerType);

        foreach (var handler in handlers)
        {
            if (handler is null) continue;
            try
            {
                var task = (Task)handlerType
                    .GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync))!
                    .Invoke(handler, [@event, ct])!;
                await task;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Domain event handler {Handler} failed for {EventType}.",
                    handler.GetType().Name, @event.GetType().Name);
            }
        }
    }
}
