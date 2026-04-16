using InventoryControl.Data;
using InventoryControl.Models;
using InventoryControl.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InventoryControl.Repositories;

public class StockMovementRepository : IStockMovementRepository
{
    private readonly AppDbContext _context;

    public StockMovementRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<StockMovement>> GetAllAsync()
        => await _context.StockMovements
            .Include(m => m.Product)
            .Include(m => m.Supplier)
            .OrderByDescending(m => m.Date)
            .ThenByDescending(m => m.Id)
            .ToListAsync();

    public async Task<PagedResult<StockMovement>> GetAllAsync(int page, int pageSize)
    {
        var query = _context.StockMovements
            .Include(m => m.Product)
            .Include(m => m.Supplier)
            .OrderByDescending(m => m.Date)
            .ThenByDescending(m => m.Id);

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<StockMovement>(items, totalCount, page, pageSize);
    }

    public async Task<IEnumerable<StockMovement>> GetByProductAsync(int productId)
        => await _context.StockMovements
            .Include(m => m.Product)
            .Include(m => m.Supplier)
            .Where(m => m.ProductId == productId)
            .OrderByDescending(m => m.Date)
            .ThenByDescending(m => m.Id)
            .ToListAsync();

    public async Task<IEnumerable<StockMovement>> GetByMonthYearAsync(int month, int year)
        => await _context.StockMovements
            .Include(m => m.Product)
            .Include(m => m.Supplier)
            .Where(m => m.Date.Month == month && m.Date.Year == year)
            .OrderByDescending(m => m.Date)
            .ThenByDescending(m => m.Id)
            .ToListAsync();

    public async Task AddAsync(StockMovement movement)
    {
        _context.StockMovements.Add(movement);
        await _context.SaveChangesAsync();
    }
}
