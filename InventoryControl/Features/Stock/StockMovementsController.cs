using InventoryControl.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace InventoryControl.Features.Stock;

public class StockMovementsController : Controller
{
    private readonly IStockMovementRepository _movementRepo;
    private readonly IProductRepository _productRepo;
    private readonly ISupplierRepository _supplierRepo;
    private readonly AppDbContext _dbContext;

    public StockMovementsController(
        IStockMovementRepository movementRepo,
        IProductRepository productRepo,
        ISupplierRepository supplierRepo,
        AppDbContext dbContext)
    {
        _movementRepo = movementRepo;
        _productRepo = productRepo;
        _supplierRepo = supplierRepo;
        _dbContext = dbContext;
    }

    public async Task<IActionResult> Index(int page = 1, int pageSize = 25)
        => View(await _movementRepo.GetAllAsync(page, pageSize));

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

        try
        {
            product.ApplyEntry(movement.Quantity);
        }
        catch (ProductArchivedException)
        {
            ModelState.AddModelError(nameof(StockMovement.ProductId),
                $"\"{product.Name}\" está arquivado. Reative-o antes de movimentar estoque.");
            await PopulateProductDropdownAsync(movement.ProductId);
            await PopulateSupplierDropdownAsync(movement.SupplierId);
            return View(movement);
        }

        _dbContext.StockMovements.Add(movement);
        await _dbContext.SaveChangesAsync();

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

        try
        {
            product.ApplyExit(movement.Quantity);
        }
        catch (InsufficientStockException ex)
        {
            ModelState.AddModelError(nameof(StockMovement.Quantity),
                $"Estoque insuficiente. Disponível: {ex.Available} unidade(s).");
            await PopulateProductDropdownAsync(movement.ProductId);
            return View(movement);
        }
        catch (ProductArchivedException)
        {
            ModelState.AddModelError(nameof(StockMovement.ProductId),
                $"\"{product.Name}\" está arquivado. Reative-o antes de movimentar estoque.");
            await PopulateProductDropdownAsync(movement.ProductId);
            return View(movement);
        }

        _dbContext.StockMovements.Add(movement);
        await _dbContext.SaveChangesAsync();

        TempData["Success"] = $"Saída de {movement.Quantity} unidade(s) de \"{product.Name}\" registrada com sucesso!";
        return RedirectToAction(nameof(Index));
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
