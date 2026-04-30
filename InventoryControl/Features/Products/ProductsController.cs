using InventoryControl.Infrastructure.Persistence;
using InventoryControl.Infrastructure.Integrations;

using InventoryControl.Validators;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace InventoryControl.Features.Products;

public class ProductsController : Controller
{
    private readonly IProductRepository _productRepo;
    private readonly ICategoryRepository _categoryRepo;
    private readonly PlatformRegistry _registry;
    private readonly IWebHostEnvironment _environment;
    private readonly AppDbContext _context;
    private readonly ProductArchiveService _archiveService;

    public ProductsController(
        IProductRepository productRepo,
        ICategoryRepository categoryRepo,
        IWebHostEnvironment environment,
        PlatformRegistry registry,
        AppDbContext context,
        ProductArchiveService archiveService)
    {
        _productRepo = productRepo;
        _categoryRepo = categoryRepo;
        _environment = environment;
        _registry = registry;
        _context = context;
        _archiveService = archiveService;
    }

    public async Task<IActionResult> Index(int page = 1, int pageSize = 20, bool showArchived = false)
    {
        ViewBag.IntegrationEnabled = _registry.GetEnabledStores().Count > 0;
        ViewBag.ShowArchived = showArchived;
        ViewBag.PendingSyncCount = await _productRepo.CountPendingSyncAsync();
        return View(await _productRepo.GetAllForListAsync(page, pageSize, includeArchived: showArchived));
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
        {
            var errors = ImageUploadValidator.Validate(images);
            if (errors.Count > 0)
            {
                foreach (var e in errors) ModelState.AddModelError("images", e);
                await PopulateDropdownsAsync(product.CategoryId);
                return View(product);
            }
            product.Images = await SaveImagesAsync(images);
        }

        try
        {
            await _productRepo.AddAsync(product);
        }
        catch (DbUpdateException ex) when (IsUniqueSkuViolation(ex, product.Sku))
        {
            ModelState.AddModelError(nameof(product.Sku),
                $"Já existe um produto com o SKU '{product.Sku}'. Escolha outro.");
            await PopulateDropdownsAsync(product.CategoryId);
            return View(product);
        }
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
            var errors = ImageUploadValidator.Validate(images);
            if (errors.Count > 0)
            {
                foreach (var e in errors) ModelState.AddModelError("images", e);
                await PopulateDropdownsAsync(product.CategoryId);
                return View(product);
            }
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

        try
        {
            await _productRepo.UpdateAsync(product);
        }
        catch (DbUpdateException ex) when (IsUniqueSkuViolation(ex, product.Sku))
        {
            ModelState.AddModelError(nameof(product.Sku),
                $"Já existe outro produto com o SKU '{product.Sku}'. Escolha outro.");
            await PopulateDropdownsAsync(product.CategoryId);
            return View(product);
        }
        TempData["Success"] = "Produto atualizado com sucesso!";
        return RedirectToAction(nameof(Index));
    }

    private static bool IsUniqueSkuViolation(DbUpdateException ex, string? sku)
    {
        if (string.IsNullOrEmpty(sku)) return false;
        // PostgreSQL: SQLSTATE 23505; SQLite: error code 19/2067 (constraint).
        // Inner message reliably mentions the SKU index name on both providers.
        var msg = (ex.InnerException?.Message ?? ex.Message);
        return msg.Contains("IX_Products_Sku", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("Products_Sku", StringComparison.OrdinalIgnoreCase);
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
            TempData["Error"] = "Não é possível excluir este produto porque existem movimentações de estoque vinculadas a ele. Considere arquivar.";
        }
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Archive(int id)
    {
        var product = await _productRepo.GetByIdAsync(id);
        if (product is null) return NotFound();
        if (product.IsArchived)
        {
            TempData["Info"] = "Este produto já está arquivado.";
            return RedirectToAction(nameof(Index));
        }
        return View(product);
    }

    [HttpPost, ActionName("Archive")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ArchiveConfirmed(int id)
    {
        var result = await _archiveService.ArchiveAsync(id);
        if (!result.Found) return NotFound();

        if (result.FullySynced)
            TempData["Success"] = "Produto arquivado com sucesso.";
        else
            TempData["Warning"] = "Produto arquivado localmente, mas falhou ao despublicar em: "
                + string.Join(", ", result.FailedStores)
                + ". Será tentado novamente em segundo plano.";

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Unarchive(int id)
    {
        var product = await _productRepo.GetByIdAsync(id);
        if (product is null) return NotFound();
        if (!product.IsArchived)
        {
            TempData["Info"] = "Este produto já está ativo.";
            return RedirectToAction(nameof(Index));
        }
        return View(product);
    }

    [HttpPost, ActionName("Unarchive")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnarchiveConfirmed(int id)
    {
        var result = await _archiveService.UnarchiveAsync(id);
        if (!result.Found) return NotFound();

        if (result.FullySynced)
            TempData["Success"] = "Produto reativado com sucesso.";
        else
            TempData["Warning"] = "Produto reativado localmente, mas falhou ao publicar em: "
                + string.Join(", ", result.FailedStores)
                + ". Será tentado novamente em segundo plano.";

        return RedirectToAction(nameof(Index), new { showArchived = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResyncPending()
    {
        var resolved = await _archiveService.RetryPendingSyncsAsync();
        var remaining = await _productRepo.CountPendingSyncAsync();

        if (resolved == 0 && remaining == 0)
            TempData["Info"] = "Nenhuma sincronização pendente.";
        else if (remaining == 0)
            TempData["Success"] = $"Sincronização concluída ({resolved} item(s) resolvido(s)).";
        else
            TempData["Warning"] = $"{resolved} item(s) resolvido(s), {remaining} ainda pendente(s).";

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

    private async Task<List<ProductImage>> SaveImagesAsync(IFormFile[] files)
    {
        var result = new List<ProductImage>();
        var uploadsDir = Path.Combine(_environment.WebRootPath, "images", "products");
        Directory.CreateDirectory(uploadsDir);

        for (var i = 0; i < files.Length; i++)
        {
            var file = files[i];
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

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
