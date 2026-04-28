namespace InventoryControl.Domain.Products;

public class ProductArchivedException : InvalidOperationException
{
    public ProductArchivedException(string productName)
        : base($"Product \"{productName}\" is archived and cannot receive stock movements. Unarchive it first.")
    {
        ProductName = productName;
    }

    public string ProductName { get; }
}
