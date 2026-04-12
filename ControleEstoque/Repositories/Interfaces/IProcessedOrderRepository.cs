using ControleEstoque.Models;

namespace ControleEstoque.Repositories.Interfaces;

public interface IProcessedOrderRepository
{
    Task<bool> ExistsAsync(string externalOrderId);
    Task<ProcessedOrder?> GetByExternalOrderIdAsync(string externalOrderId);
    Task AddAsync(ProcessedOrder processedOrder);
    Task UpdateAsync(ProcessedOrder processedOrder);
}
