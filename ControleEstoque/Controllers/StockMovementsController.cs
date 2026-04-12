using ControleEstoque.Integrations;
using ControleEstoque.Models;
using ControleEstoque.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ControleEstoque.Controllers;

public class StockMovementsController : Controller
{
    private readonly IStockMovementRepository _movementRepo;
    private readonly IProductRepository _productRepo;
    private readonly ISupplierRepository _supplierRepo;
    private readonly SyncService? _syncService;
    private readonly ILogger<StockMovementsController> _logger;

    public StockMovementsController(
        IStockMovementRepository movementRepo,
        IProductRepository productRepo,
        ISupplierRepository supplierRepo,
        ILogger<StockMovementsController> logger,
        SyncService? syncService = null)
    {
        _movementRepo = movementRepo;
        _productRepo = productRepo;
        _supplierRepo = supplierRepo;
        _logger = logger;
        _syncService = syncService;
    }

    public async Task<IActionResult> Index()
        => View(await _movementRepo.GetAllAsync());

    // GET: StockMovements/Entry
    public async Task<IActionResult> Entry()
    {
        await PopulateProductDropdownAsync();
        await PopulateSupplierDropdownAsync();
        return View(new StockMovement { Type = MovementType.Entry, Date = DateTime.Today });
    }

    // POST: StockMovements/Entry
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Entry(StockMovement movement)
    {
        movement.Type = MovementType.Entry;
        ModelState.Remove(nameof(StockMovement.ExitReason));

        if (!ModelState.IsValid)
        {
            await PopulateProductDropdownAsync(movement.ProductId);
            await PopulateSupplierDropdownAsync(movement.SupplierId);
            return View(movement);
        }

        var product = await _productRepo.GetByIdAsync(movement.ProductId);
        if (product is null) return NotFound();

        await _movementRepo.AddAsync(movement);
        await _productRepo.UpdateStockAsync(product.Id, product.CurrentStock + movement.Quantity);

        await TryPushStockAsync(product.Id, product.Name);

        TempData["Success"] = $"Entrada de {movement.Quantity} unidade(s) de \"{product.Name}\" registrada com sucesso!";
        return RedirectToAction(nameof(Index));
    }

    // GET: StockMovements/Exit
    public async Task<IActionResult> Exit()
    {
        await PopulateProductDropdownAsync();
        return View(new StockMovement { Type = MovementType.Exit, Date = DateTime.Today });
    }

    // POST: StockMovements/Exit
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Exit(StockMovement movement)
    {
        movement.Type = MovementType.Exit;
        ModelState.Remove(nameof(StockMovement.SupplierId));
        ModelState.Remove(nameof(StockMovement.UnitCost));

        if (!ModelState.IsValid)
        {
            await PopulateProductDropdownAsync(movement.ProductId);
            return View(movement);
        }

        var product = await _productRepo.GetByIdAsync(movement.ProductId);
        if (product is null) return NotFound();

        if (product.CurrentStock < movement.Quantity)
        {
            ModelState.AddModelError(nameof(StockMovement.Quantity),
                $"Estoque insuficiente. Disponível: {product.CurrentStock} unidade(s).");
            await PopulateProductDropdownAsync(movement.ProductId);
            return View(movement);
        }

        await _movementRepo.AddAsync(movement);
        await _productRepo.UpdateStockAsync(product.Id, product.CurrentStock - movement.Quantity);

        await TryPushStockAsync(product.Id, product.Name);

        TempData["Success"] = $"Saída de {movement.Quantity} unidade(s) de \"{product.Name}\" registrada com sucesso!";
        return RedirectToAction(nameof(Index));
    }

    private async Task TryPushStockAsync(int productId, string productName)
    {
        if (_syncService is null) return;
        try
        {
            await _syncService.PushStockToStoreAsync(productId);
            _logger.LogInformation(
                "Auto-pushed stock for product {ProductName} (id={ProductId}) after movement.",
                productName, productId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to auto-push stock for product {ProductName} (id={ProductId}) after movement.",
                productName, productId);
        }
    }

    private async Task PopulateProductDropdownAsync(int? selectedId = null)
    {
        var products = await _productRepo.GetAllAsync();
        ViewBag.ProductId = new SelectList(products, "Id", "Name", selectedId);
    }

    private async Task PopulateSupplierDropdownAsync(int? selectedId = null)
    {
        var suppliers = await _supplierRepo.GetAllAsync();
        ViewBag.SupplierId = new SelectList(suppliers, "Id", "Name", selectedId);
    }
}
