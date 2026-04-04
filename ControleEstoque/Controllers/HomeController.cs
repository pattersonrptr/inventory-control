using ControleEstoque.Integrations.Abstractions;
using ControleEstoque.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ControleEstoque.Controllers;

public class HomeController : Controller
{
    private readonly IProductRepository _productRepo;
    private readonly IStockMovementRepository _movementRepo;
    private readonly IntegrationConfig? _integrationConfig;

    public HomeController(
        IProductRepository productRepo,
        IStockMovementRepository movementRepo,
        IntegrationConfig? integrationConfig = null)
    {
        _productRepo = productRepo;
        _movementRepo = movementRepo;
        _integrationConfig = integrationConfig;
    }

    public async Task<IActionResult> Index()
    {
        var products = await _productRepo.GetAllAsync();
        var belowMinimum = await _productRepo.GetBelowMinimumAsync();
        var movements = await _movementRepo.GetAllAsync();

        ViewBag.TotalProducts = products.Count();
        ViewBag.ProductsBelowMinimum = belowMinimum.Count();
        ViewBag.RecentMovements = movements.Take(5).ToList();
        ViewBag.IntegrationEnabled = _integrationConfig?.Enabled == true;

        return View();
    }
}
