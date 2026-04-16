using InventoryControl.Models;
using InventoryControl.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace InventoryControl.Controllers;

public class CategoriesController : Controller
{
    private readonly ICategoryRepository _repository;

    public CategoriesController(ICategoryRepository repository)
    {
        _repository = repository;
    }

    public async Task<IActionResult> Index(int page = 1, int pageSize = 20)
        => View(await _repository.GetAllAsync(page, pageSize));

    public async Task<IActionResult> Create()
    {
        await PopulateParentDropdownAsync();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Category category)
    {
        if (!ModelState.IsValid)
        {
            await PopulateParentDropdownAsync(category.ParentId);
            return View(category);
        }

        await _repository.AddAsync(category);
        TempData["Success"] = "Categoria criada com sucesso!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateInline([FromBody] Category category)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });

        await _repository.AddAsync(category);

        var saved = await _repository.GetByIdAsync(category.Id);
        return StatusCode(201, new { id = category.Id, name = category.Name, fullName = saved?.FullName ?? category.Name });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var category = await _repository.GetByIdAsync(id);
        if (category is null) return NotFound();
        await PopulateParentDropdownAsync(category.ParentId, excludeId: id);
        return View(category);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Category category)
    {
        if (id != category.Id) return BadRequest();
        if (!ModelState.IsValid)
        {
            await PopulateParentDropdownAsync(category.ParentId, excludeId: id);
            return View(category);
        }

        if (category.ParentId == category.Id)
        {
            ModelState.AddModelError("ParentId", "Uma categoria não pode ser pai de si mesma.");
            await PopulateParentDropdownAsync(category.ParentId, excludeId: id);
            return View(category);
        }

        await _repository.UpdateAsync(category);
        TempData["Success"] = "Categoria atualizada com sucesso!";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var category = await _repository.GetByIdAsync(id);
        if (category is null) return NotFound();
        return View(category);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        try
        {
            await _repository.DeleteAsync(id);
            TempData["Success"] = "Categoria excluída com sucesso!";
        }
        catch (DbUpdateException)
        {
            TempData["Error"] = "Não é possível excluir esta categoria porque existem produtos ou subcategorias vinculadas a ela.";
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateParentDropdownAsync(int? selectedParentId = null, int? excludeId = null)
    {
        var categories = await _repository.GetAllAsync();
        var items = categories
            .Where(c => excludeId is null || c.Id != excludeId)
            .Select(c => new { c.Id, Name = c.FullName });
        ViewBag.ParentId = new SelectList(items, "Id", "Name", selectedParentId);
    }
}
