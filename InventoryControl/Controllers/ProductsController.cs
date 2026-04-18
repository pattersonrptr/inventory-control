using InventoryControl.Data;
using InventoryControl.Integrations;
using InventoryControl.Models;
using InventoryControl.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace InventoryControl.Controllers;

public class ProductsController : Controller
{
    private readonly IProductRepository _productRepo;
    private readonly ICategoryRepository _categoryRepo;
    private readonly PlatformRegistry _registry;
    private readonly IWebHostEnvironment _environment;
    private readonly AppDbContext _context;

    public ProductsController(
        IProductRepository productRepo,
        ICategoryRepository categoryRepo,
        IWebHostEnvironment environment,
        PlatformRegistry registry,
        AppDbContext context)
    {
        _productRepo = productRepo;
        _categoryRepo = categoryRepo;
        _environment = environment;
        _registry = registry;
        _context = context;
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
    public async Task<IActionResult> Create(Product product, IFormFile[]? images)
    {
        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync(product.CategoryId);
            return View(product);
        }

        if (images is not null)
            product.Images = await SaveImagesAsync(images);

        await _productRepo.AddAsync(product);
        TempData["Success"] = "Produto criado com sucesso!";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var product = await _productRepo.GetByIdAsync(id);
        if (product is null) return NotFound();
        await PopulateDropdownsAsync(product.CategoryId);
        return View(product);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Product product, IFormFile[]? images)
    {
        if (id != product.Id) return BadRequest();
        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync(product.CategoryId);
            return View(product);
        }

        if (images is { Length: > 0 })
        {
            var newImages = await SaveImagesAsync(images);
            var hasPrimary = await _context.ProductImages.AnyAsync(pi => pi.ProductId == id && pi.IsPrimary);
            if (!hasPrimary && newImages.Count > 0)
                newImages[0].IsPrimary = true;

            foreach (var img in newImages)
            {
                img.ProductId = id;
                _context.ProductImages.Add(img);
            }
            await _context.SaveChangesAsync();
        }

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

    private async Task PopulateDropdownsAsync(int? categoryId = null)
    {
        var categories = await _categoryRepo.GetAllAsync();
        ViewBag.CategoryId = new SelectList(
            categories.Select(c => new { c.Id, Name = c.FullName }),
            "Id", "Name", categoryId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteImage(int id)
    {
        var image = await _context.ProductImages.FindAsync(id);
        if (image is null) return NotFound();

        var productId = image.ProductId;
        var filePath = Path.Combine(_environment.WebRootPath, image.ImagePath.TrimStart('/'));
        if (System.IO.File.Exists(filePath))
            System.IO.File.Delete(filePath);

        _context.ProductImages.Remove(image);

        if (image.IsPrimary)
        {
            var next = await _context.ProductImages
                .Where(pi => pi.ProductId == productId && pi.Id != id)
                .OrderBy(pi => pi.DisplayOrder)
                .FirstOrDefaultAsync();
            if (next is not null)
                next.IsPrimary = true;
        }

        await _context.SaveChangesAsync();
        return Json(new { success = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPrimaryImage(int id)
    {
        var image = await _context.ProductImages.FindAsync(id);
        if (image is null) return NotFound();

        var allImages = await _context.ProductImages
            .Where(pi => pi.ProductId == image.ProductId)
            .ToListAsync();

        foreach (var img in allImages)
            img.IsPrimary = img.Id == id;

        await _context.SaveChangesAsync();
        return Json(new { success = true });
    }

    private static readonly HashSet<string> AllowedExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp"];

    private async Task<List<ProductImage>> SaveImagesAsync(IFormFile[] files)
    {
        var result = new List<ProductImage>();
        var uploadsDir = Path.Combine(_environment.WebRootPath, "images", "products");
        Directory.CreateDirectory(uploadsDir);

        for (var i = 0; i < files.Length; i++)
        {
            var file = files[i];
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension))
                extension = ".jpg";

            var fileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadsDir, fileName);
            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            result.Add(new ProductImage
            {
                ImagePath = $"/images/products/{fileName}",
                AltText = Path.GetFileNameWithoutExtension(file.FileName),
                DisplayOrder = i,
                IsPrimary = i == 0
            });
        }

        return result;
    }
}
