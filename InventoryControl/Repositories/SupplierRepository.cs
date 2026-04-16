using InventoryControl.Data;
using InventoryControl.Models;
using InventoryControl.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InventoryControl.Repositories;

public class SupplierRepository : ISupplierRepository
{
    private readonly AppDbContext _context;

    public SupplierRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Supplier>> GetAllAsync()
        => await _context.Suppliers
            .Include(s => s.Products)
            .OrderBy(s => s.Name)
            .ToListAsync();

    public async Task<PagedResult<Supplier>> GetAllAsync(int page, int pageSize)
    {
        var query = _context.Suppliers
            .Include(s => s.Products)
            .OrderBy(s => s.Name);

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<Supplier>(items, totalCount, page, pageSize);
    }

    public async Task<Supplier?> GetByIdAsync(int id)
        => await _context.Suppliers.FindAsync(id);

    public async Task AddAsync(Supplier supplier)
    {
        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Supplier supplier)
    {
        _context.Suppliers.Update(supplier);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var supplier = await _context.Suppliers.FindAsync(id);
        if (supplier is not null)
        {
            _context.Suppliers.Remove(supplier);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(int id)
        => await _context.Suppliers.AnyAsync(s => s.Id == id);
}
