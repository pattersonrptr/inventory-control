using InventoryControl.Models;

namespace InventoryControl.Repositories.Interfaces;

public interface IStockMovementRepository
{
    Task<IEnumerable<StockMovement>> GetAllAsync();
    Task<PagedResult<StockMovement>> GetAllAsync(int page, int pageSize);
    Task<IEnumerable<StockMovement>> GetByProductAsync(int productId);
    Task<IEnumerable<StockMovement>> GetByMonthYearAsync(int month, int year);
    Task AddAsync(StockMovement movement);
}
