using InventoryControl.Integrations;
using InventoryControl.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryControl.Controllers;

public class HomeController : Controller
{
    private readonly IProductRepository _productRepo;
    private readonly IStockMovementRepository _movementRepo;
    private readonly ICategoryRepository _categoryRepo;
    private readonly PlatformRegistry _registry;

    public HomeController(
        IProductRepository productRepo,
        IStockMovementRepository movementRepo,
        ICategoryRepository categoryRepo,
        PlatformRegistry registry)
    {
        _productRepo = productRepo;
        _movementRepo = movementRepo;
        _categoryRepo = categoryRepo;
        _registry = registry;
    }

    public async Task<IActionResult> Index()
    {
        var products = await _productRepo.GetAllAsync();
        var belowMinimum = await _productRepo.GetBelowMinimumAsync();
        var movements = await _movementRepo.GetAllAsync();

        ViewBag.TotalProducts = products.Count();
        ViewBag.ProductsBelowMinimum = belowMinimum.Count();
        ViewBag.RecentMovements = movements.Take(5).ToList();
        ViewBag.IntegrationEnabled = _registry.GetEnabledStores().Count > 0;

        return View();
    }

    [Authorize]
    [HttpGet]
    [Route("/api/dashboard/movements-by-month")]
    public async Task<IActionResult> MovementsByMonth()
    {
        var movements = await _movementRepo.GetAllAsync();
        var sixMonthsAgo = DateTime.Today.AddMonths(-5);
        var startOfRange = new DateTime(sixMonthsAgo.Year, sixMonthsAgo.Month, 1);

        var data = movements
            .Where(m => m.Date >= startOfRange)
            .GroupBy(m => new { m.Date.Year, m.Date.Month })
            .Select(g => new
            {
                month = $"{g.Key.Month:00}/{g.Key.Year}",
                sortKey = g.Key.Year * 100 + g.Key.Month,
                entries = g.Where(m => m.Type == MovementType.Entry).Sum(m => m.Quantity),
                exits = g.Where(m => m.Type == MovementType.Exit).Sum(m => m.Quantity)
            })
            .OrderBy(x => x.sortKey)
            .ToList();

        return Json(data);
    }

    [Authorize]
    [HttpGet]
    [Route("/api/dashboard/top-sellers")]
    public async Task<IActionResult> TopSellers()
    {
        var movements = await _movementRepo.GetAllAsync();
        var data = movements
            .Where(m => m.Type == MovementType.Exit)
            .GroupBy(m => m.Product?.Name ?? "Desconhecido")
            .Select(g => new
            {
                product = g.Key,
                quantity = g.Sum(m => m.Quantity)
            })
            .OrderByDescending(x => x.quantity)
            .Take(10)
            .ToList();

        return Json(data);
    }

    [Authorize]
    [HttpGet]
    [Route("/api/dashboard/stock-by-category")]
    public async Task<IActionResult> StockByCategory()
    {
        var products = await _productRepo.GetAllAsync();
        var data = products
            .GroupBy(p => p.Category?.Name ?? "Sem Categoria")
            .Select(g => new
            {
                category = g.Key,
                stock = g.Sum(p => p.CurrentStock)
            })
            .OrderByDescending(x => x.stock)
            .ToList();

        return Json(data);
    }

    [Authorize]
    [HttpGet]
    [Route("/api/movements/recent")]
    public async Task<IActionResult> RecentMovements()
    {
        var movements = await _movementRepo.GetAllAsync();
        var recent = movements.Take(5).Select(m => new
        {
            date = m.Date.ToString("dd/MM/yyyy"),
            product = m.Product?.Name,
            type = m.Type == MovementType.Entry ? "entry" : "exit",
            quantity = m.Quantity
        });

        return Json(recent);
    }
}
