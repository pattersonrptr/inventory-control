using ControleEstoque.Data;
using ControleEstoque.Models;
using ControleEstoque.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ControleEstoque.Repositories;

public class ProcessedOrderRepository : IProcessedOrderRepository
{
    private readonly AppDbContext _context;

    public ProcessedOrderRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> ExistsAsync(string externalOrderId)
        => await _context.ProcessedOrders.AnyAsync(o => o.ExternalOrderId == externalOrderId);

    public async Task AddAsync(ProcessedOrder processedOrder)
    {
        _context.ProcessedOrders.Add(processedOrder);
        await _context.SaveChangesAsync();
    }
}
