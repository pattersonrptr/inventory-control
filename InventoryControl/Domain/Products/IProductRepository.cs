
namespace InventoryControl.Domain.Products;

public interface IProductRepository
{
    Task<IEnumerable<Product>> GetAllAsync();
    Task<PagedResult<Product>> GetAllAsync(int page, int pageSize);
    Task<PagedResult<Product>> GetAllForListAsync(int page, int pageSize);
    Task<Product?> GetByIdAsync(int id);
    Task<IEnumerable<Product>> GetBelowMinimumAsync();
    Task AddAsync(Product product);
    Task UpdateAsync(Product product);
    Task DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
    Task UpdateStockAsync(int productId, int newQuantity);
}
