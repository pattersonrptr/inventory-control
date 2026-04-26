namespace InventoryControl.Domain.Products;

public class InsufficientStockException : InvalidOperationException
{
    public InsufficientStockException(string productName, int available, int requested)
        : base($"Insufficient stock for \"{productName}\": available={available}, requested={requested}.")
    {
        ProductName = productName;
        Available = available;
        Requested = requested;
    }

    public string ProductName { get; }
    public int Available { get; }
    public int Requested { get; }
}
