using ControleEstoque.Models;
using ControleEstoque.Repositories.Interfaces;
using ControleEstoque.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace ControleEstoque.Controllers;

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
}
