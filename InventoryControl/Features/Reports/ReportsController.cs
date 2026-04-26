
using InventoryControl.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace InventoryControl.Features.Reports;

public class ReportsController : Controller
{
    private readonly IProductRepository _productRepo;
    private readonly IStockMovementRepository _movementRepo;

    public ReportsController(IProductRepository productRepo, IStockMovementRepository movementRepo)
    {
        _productRepo = productRepo;
        _movementRepo = movementRepo;
    }

    public async Task<IActionResult> BelowMinimum()
    {
        var products = await _productRepo.GetBelowMinimumAsync();
        return View(products);
    }

    public async Task<IActionResult> Monthly(int? month, int? year)
    {
        var currentMonth = month ?? DateTime.Today.Month;
        var currentYear = year ?? DateTime.Today.Year;

        var movements = await _movementRepo.GetByMonthYearAsync(currentMonth, currentYear);

        var items = movements
            .GroupBy(m => m.Product.Name)
            .Select(g => new MonthlyReportItem
            {
                ProductName = g.Key,
                TotalEntries = g.Where(m => m.Type == MovementType.Entry).Sum(m => m.Quantity),
                TotalExits = g.Where(m => m.Type == MovementType.Exit).Sum(m => m.Quantity)
            })
            .OrderBy(i => i.ProductName)
            .ToList();

        var viewModel = new MonthlyReportViewModel
        {
            Month = currentMonth,
            Year = currentYear,
            Items = items
        };

        return View(viewModel);
    }

    public async Task<IActionResult> Profitability(int? month, int? year)
    {
        var currentMonth = month ?? DateTime.Today.Month;
        var currentYear = year ?? DateTime.Today.Year;

        var movements = await _movementRepo.GetByMonthYearAsync(currentMonth, currentYear);
        var products = await _productRepo.GetAllAsync();
        var productLookup = products.ToDictionary(p => p.Id);

        var items = movements
            .Where(m => m.Type == MovementType.Exit)
            .GroupBy(m => m.ProductId)
            .Select(g =>
            {
                productLookup.TryGetValue(g.Key, out var product);
                return new ProfitabilityItem
                {
                    ProductName = product?.Name ?? g.First().Product?.Name ?? "Desconhecido",
                    QuantitySold = g.Sum(m => m.Quantity),
                    SellingPrice = product?.SellingPrice ?? 0,
                    CostPrice = product?.CostPrice ?? 0
                };
            })
            .OrderByDescending(i => i.Profit)
            .ToList();

        var viewModel = new ProfitabilityReportViewModel
        {
            Month = currentMonth,
            Year = currentYear,
            Items = items
        };

        return View(viewModel);
    }
}
