using InventoryControl.Data;
using InventoryControl.Models;
using InventoryControl.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InventoryControl.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly AppDbContext _context;

    public ProductRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Product>> GetAllAsync()
        => await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Include(p => p.ExternalMappings)
            .OrderBy(p => p.Name)
            .ToListAsync();

    public async Task<PagedResult<Product>> GetAllAsync(int page, int pageSize)
    {
        var query = _context.Products
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Include(p => p.ExternalMappings)
            .OrderBy(p => p.Name);

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<Product>(items, totalCount, page, pageSize);
    }

    public async Task<Product?> GetByIdAsync(int id)
        => await _context.Products
            .Include(p => p.Category)
            .Include(p => p.StockMovements)
            .Include(p => p.Images.OrderBy(i => i.DisplayOrder))
            .Include(p => p.ExternalMappings)
            .FirstOrDefaultAsync(p => p.Id == id);

    public async Task<IEnumerable<Product>> GetBelowMinimumAsync()
        => await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Where(p => p.CurrentStock <= p.MinimumStock)
            .OrderBy(p => p.Name)
            .ToListAsync();

    public async Task AddAsync(Product product)
    {
        _context.Products.Add(product);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Product product)
    {
        _context.Products.Update(product);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product is not null)
        {
            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(int id)
        => await _context.Products.AnyAsync(p => p.Id == id);

    public async Task UpdateStockAsync(int productId, int newQuantity)
    {
        var product = await _context.Products.FindAsync(productId);
        if (product is not null)
        {
            product.CurrentStock = newQuantity;
            await _context.SaveChangesAsync();
        }
    }
}
