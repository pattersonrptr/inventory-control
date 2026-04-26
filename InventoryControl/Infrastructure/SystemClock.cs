namespace InventoryControl.Infrastructure;

public class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
