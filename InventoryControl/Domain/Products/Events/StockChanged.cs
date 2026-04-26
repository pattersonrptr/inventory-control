using InventoryControl.Domain.Shared;

namespace InventoryControl.Domain.Products.Events;

public record StockChanged(int ProductId, int NewStock) : IDomainEvent;
