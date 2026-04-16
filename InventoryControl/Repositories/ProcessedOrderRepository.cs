using InventoryControl.Data;
using InventoryControl.Models;
using InventoryControl.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InventoryControl.Repositories;

public class ProcessedOrderRepository : IProcessedOrderRepository
{
    private readonly AppDbContext _context;

    public ProcessedOrderRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> ExistsAsync(string externalOrderId)
        => await _context.ProcessedOrders.AnyAsync(o => o.ExternalOrderId == externalOrderId);

    public async Task<ProcessedOrder?> GetByExternalOrderIdAsync(string externalOrderId)
        => await _context.ProcessedOrders.FirstOrDefaultAsync(o => o.ExternalOrderId == externalOrderId);

    public async Task AddAsync(ProcessedOrder processedOrder)
    {
        _context.ProcessedOrders.Add(processedOrder);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(ProcessedOrder processedOrder)
    {
        _context.ProcessedOrders.Update(processedOrder);
        await _context.SaveChangesAsync();
    }
}
