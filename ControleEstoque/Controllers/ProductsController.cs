using ControleEstoque.Integrations;
using ControleEstoque.Models;
using ControleEstoque.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ControleEstoque.Controllers;

public class ProductsController : Controller
{
    private readonly IProductRepository _productRepo;
    private readonly ICategoryRepository _categoryRepo;
    private readonly ISupplierRepository _supplierRepo;
    private readonly PlatformRegistry _registry;
    private readonly IWebHostEnvironment _environment;

    public ProductsController(
        IProductRepository productRepo,
        ICategoryRepository categoryRepo,
        ISupplierRepository supplierRepo,
        IWebHostEnvironment environment,
        PlatformRegistry registry)
    {
        _productRepo = productRepo;
        _categoryRepo = categoryRepo;
        _supplierRepo = supplierRepo;
        _environment = environment;
        _registry = registry;
    }

    public async Task<IActionResult> Index(int page = 1, int pageSize = 20)
    {
        ViewBag.IntegrationEnabled = _registry.GetEnabledStores().Count > 0;
        return View(await _productRepo.GetAllAsync(page, pageSize));
    }

    public async Task<IActionResult> Details(int id)
    {
        var product = await _productRepo.GetByIdAsync(id);
        if (product is null) return NotFound();
        return View(product);
    }

    public async Task<IActionResult> Create()
    {
        await PopulateDropdownsAsync();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Product product, IFormFile? image)
    {
        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync(product.CategoryId, product.SupplierId);
            return View(product);
        }

        if (image is not null)
            product.ImagePath = await SaveImageAsync(image);

        await _productRepo.AddAsync(product);
        TempData["Success"] = "Produto criado com sucesso!";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var product = await _productRepo.GetByIdAsync(id);
        if (product is null) return NotFound();
        await PopulateDropdownsAsync(product.CategoryId, product.SupplierId);
        return View(product);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Product product, IFormFile? image)
    {
        if (id != product.Id) return BadRequest();
        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync(product.CategoryId, product.SupplierId);
            return View(product);
        }

        if (image is not null)
            product.ImagePath = await SaveImageAsync(image);

        await _productRepo.UpdateAsync(product);
        TempData["Success"] = "Produto atualizado com sucesso!";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var product = await _productRepo.GetByIdAsync(id);
        if (product is null) return NotFound();
        return View(product);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        try
        {
            await _productRepo.DeleteAsync(id);
            TempData["Success"] = "Produto excluído com sucesso!";
        }
        catch (DbUpdateException)
        {
            TempData["Error"] = "Não é possível excluir este produto porque existem movimentações de estoque vinculadas a ele.";
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateDropdownsAsync(int? categoryId = null, int? supplierId = null)
    {
        var categories = await _categoryRepo.GetAllAsync();
        var suppliers = await _supplierRepo.GetAllAsync();
        ViewBag.CategoryId = new SelectList(categories, "Id", "Name", categoryId);
        ViewBag.SupplierId = new SelectList(suppliers, "Id", "Name", supplierId);
    }

    private static readonly HashSet<string> AllowedExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp"];

    private async Task<string> SaveImageAsync(IFormFile image)
    {
        var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            extension = ".jpg";

        var fileName = $"{Guid.NewGuid()}{extension}";
        var uploadsDir = Path.Combine(_environment.WebRootPath, "images", "products");
        Directory.CreateDirectory(uploadsDir);

        var filePath = Path.Combine(uploadsDir, fileName);
        using var stream = new FileStream(filePath, FileMode.Create);
        await image.CopyToAsync(stream);

        return $"/images/products/{fileName}";
    }
}
