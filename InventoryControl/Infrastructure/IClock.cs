namespace InventoryControl.Infrastructure;

public interface IClock
{
    DateTime UtcNow { get; }
}
