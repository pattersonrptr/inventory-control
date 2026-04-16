using InventoryControl.Models;
using InventoryControl.Repositories.Interfaces;
using InventoryControl.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryControl.Controllers;

[Authorize(Roles = "Admin")]
public class ImportController : Controller
{
    private readonly IProductRepository _productRepo;
    private readonly ICategoryRepository _categoryRepo;
    private readonly ISupplierRepository _supplierRepo;

    public ImportController(
        IProductRepository productRepo,
        ICategoryRepository categoryRepo,
        ISupplierRepository supplierRepo)
    {
        _productRepo = productRepo;
        _categoryRepo = categoryRepo;
        _supplierRepo = supplierRepo;
    }

    public IActionResult Products() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PreviewProducts(IFormFile file)
    {
        if (file is null || file.Length == 0)
        {
            TempData["Error"] = "Selecione um arquivo CSV.";
            return RedirectToAction(nameof(Products));
        }

        var categories = await _categoryRepo.GetAllAsync();
        var suppliers = await _supplierRepo.GetAllAsync();

        using var stream = file.OpenReadStream();
        var result = CsvImportService.ParseProducts(stream, categories, suppliers);

        ViewBag.Result = result;
        return View("Products");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmProducts(string productsJson)
    {
        if (string.IsNullOrWhiteSpace(productsJson))
        {
            TempData["Error"] = "Nenhum dado para importar.";
            return RedirectToAction(nameof(Products));
        }

        var products = System.Text.Json.JsonSerializer.Deserialize<List<Product>>(productsJson);
        if (products is null || products.Count == 0)
        {
            TempData["Error"] = "Nenhum produto válido para importar.";
            return RedirectToAction(nameof(Products));
        }

        int imported = 0;
        foreach (var product in products)
        {
            // Reset navigation properties and Id for new entities
            product.Id = 0;
            product.Category = null!;
            product.Supplier = null!;
            await _productRepo.AddAsync(product);
            imported++;
        }

        TempData["Success"] = $"{imported} produto(s) importado(s) com sucesso!";
        return RedirectToAction("Index", "Products");
    }

    public IActionResult Categories() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult PreviewCategories(IFormFile file)
    {
        if (file is null || file.Length == 0)
        {
            TempData["Error"] = "Selecione um arquivo CSV.";
            return RedirectToAction(nameof(Categories));
        }

        using var stream = file.OpenReadStream();
        var result = CsvImportService.ParseCategories(stream);

        ViewBag.Result = result;
        return View("Categories");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmCategories(string categoriesJson)
    {
        if (string.IsNullOrWhiteSpace(categoriesJson))
        {
            TempData["Error"] = "Nenhum dado para importar.";
            return RedirectToAction(nameof(Categories));
        }

        var categories = System.Text.Json.JsonSerializer.Deserialize<List<Category>>(categoriesJson);
        if (categories is null || categories.Count == 0)
        {
            TempData["Error"] = "Nenhuma categoria válida para importar.";
            return RedirectToAction(nameof(Categories));
        }

        int imported = 0;
        foreach (var category in categories)
        {
            category.Id = 0;
            await _categoryRepo.AddAsync(category);
            imported++;
        }

        TempData["Success"] = $"{imported} categoria(s) importada(s) com sucesso!";
        return RedirectToAction("Index", "Categories");
    }

    public IActionResult Suppliers() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult PreviewSuppliers(IFormFile file)
    {
        if (file is null || file.Length == 0)
        {
            TempData["Error"] = "Selecione um arquivo CSV.";
            return RedirectToAction(nameof(Suppliers));
        }

        using var stream = file.OpenReadStream();
        var result = CsvImportService.ParseSuppliers(stream);

        ViewBag.Result = result;
        return View("Suppliers");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmSuppliers(string suppliersJson)
    {
        if (string.IsNullOrWhiteSpace(suppliersJson))
        {
            TempData["Error"] = "Nenhum dado para importar.";
            return RedirectToAction(nameof(Suppliers));
        }

        var suppliers = System.Text.Json.JsonSerializer.Deserialize<List<Supplier>>(suppliersJson);
        if (suppliers is null || suppliers.Count == 0)
        {
            TempData["Error"] = "Nenhum fornecedor válido para importar.";
            return RedirectToAction(nameof(Suppliers));
        }

        int imported = 0;
        foreach (var supplier in suppliers)
        {
            supplier.Id = 0;
            await _supplierRepo.AddAsync(supplier);
            imported++;
        }

        TempData["Success"] = $"{imported} fornecedor(es) importado(s) com sucesso!";
        return RedirectToAction("Index", "Suppliers");
    }
}
