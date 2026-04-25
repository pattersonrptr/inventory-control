
namespace InventoryControl.Repositories.Interfaces;

public interface ISupplierRepository
{
    Task<IEnumerable<Supplier>> GetAllAsync();
    Task<PagedResult<Supplier>> GetAllAsync(int page, int pageSize);
    Task<Supplier?> GetByIdAsync(int id);
    Task AddAsync(Supplier supplier);
    Task UpdateAsync(Supplier supplier);
    Task DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
}
