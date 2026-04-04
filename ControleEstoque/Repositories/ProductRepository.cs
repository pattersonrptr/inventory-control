using ControleEstoque.Data;
using ControleEstoque.Models;
using ControleEstoque.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ControleEstoque.Repositories;

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
            .Include(p => p.Supplier)
            .OrderBy(p => p.Name)
            .ToListAsync();

    public async Task<Product?> GetByIdAsync(int id)
        => await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Supplier)
            .Include(p => p.StockMovements)
            .FirstOrDefaultAsync(p => p.Id == id);

    public async Task<IEnumerable<Product>> GetBelowMinimumAsync()
        => await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Supplier)
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
