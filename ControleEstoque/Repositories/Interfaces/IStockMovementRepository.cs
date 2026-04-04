using ControleEstoque.Models;

namespace ControleEstoque.Repositories.Interfaces;

public interface IStockMovementRepository
{
    Task<IEnumerable<StockMovement>> GetAllAsync();
    Task<IEnumerable<StockMovement>> GetByProductAsync(int productId);
    Task<IEnumerable<StockMovement>> GetByMonthYearAsync(int month, int year);
    Task AddAsync(StockMovement movement);
}
