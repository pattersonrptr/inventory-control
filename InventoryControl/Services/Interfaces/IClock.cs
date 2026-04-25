namespace InventoryControl.Services.Interfaces;

public interface IClock
{
    DateTime UtcNow { get; }
}
