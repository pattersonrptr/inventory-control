using InventoryControl.Data;
using InventoryControl.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InventoryControl.Repositories;

public class CategoryRepository : ICategoryRepository
{
    private readonly AppDbContext _context;

    public CategoryRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Category>> GetAllAsync()
        => await _context.Categories
            .Include(c => c.Products)
            .Include(c => c.Parent)
            .Include(c => c.Children)
            .Include(c => c.ExternalMappings)
            .OrderBy(c => c.Name)
            .ToListAsync();

    public async Task<PagedResult<Category>> GetAllAsync(int page, int pageSize)
    {
        var baseQuery = _context.Categories.OrderBy(c => c.Name);
        var totalCount = await baseQuery.CountAsync();
        var items = await baseQuery
            .Include(c => c.Products)
            .Include(c => c.Parent)
            .Include(c => c.Children)
            .Include(c => c.ExternalMappings)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsSplitQuery()
            .ToListAsync();

        return new PagedResult<Category>(items, totalCount, page, pageSize);
    }

    public async Task<PagedResult<Category>> GetAllForListAsync(int page, int pageSize)
    {
        var baseQuery = _context.Categories.OrderBy(c => c.Name);
        var totalCount = await baseQuery.CountAsync();
        var items = await baseQuery
            .Include(c => c.Parent)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<Category>(items, totalCount, page, pageSize);
    }

    public async Task<Category?> GetByIdAsync(int id)
        => await _context.Categories.FindAsync(id);

    public async Task AddAsync(Category category)
    {
        _context.Categories.Add(category);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Category category)
    {
        _context.Categories.Update(category);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category is not null)
        {
            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(int id)
        => await _context.Categories.AnyAsync(c => c.Id == id);
}
