using ControleEstoque.Integrations.Abstractions;
using ControleEstoque.Models;
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
