using InventoryControl.Models;

namespace InventoryControl.Repositories.Interfaces;

public interface ICategoryRepository
{
    Task<IEnumerable<Category>> GetAllAsync();
    Task<PagedResult<Category>> GetAllAsync(int page, int pageSize);
    Task<PagedResult<Category>> GetAllForListAsync(int page, int pageSize);
    Task<Category?> GetByIdAsync(int id);
    Task AddAsync(Category category);
    Task UpdateAsync(Category category);
    Task DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
}
