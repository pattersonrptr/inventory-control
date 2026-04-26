using InventoryControl.Domain.Shared;

namespace InventoryControl.Domain.Products.Events;

public record ProductWentBelowMinimum(
    int ProductId,
    string ProductName,
    int CurrentStock,
    int MinimumStock) : IDomainEvent;
