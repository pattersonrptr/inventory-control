using InventoryControl.Services.Interfaces;

namespace InventoryControl.Services;

public class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
