using InventoryControl.Models;
using InventoryControl.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;
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

    public IActionResult Create() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Category category)
    {
        if (!ModelState.IsValid) return View(category);

        await _repository.AddAsync(category);
        TempData["Success"] = "Categoria criada com sucesso!";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var category = await _repository.GetByIdAsync(id);
        if (category is null) return NotFound();
        return View(category);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Category category)
    {
        if (id != category.Id) return BadRequest();
        if (!ModelState.IsValid) return View(category);

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
            TempData["Error"] = "Não é possível excluir esta categoria porque existem produtos vinculados a ela.";
        }
        return RedirectToAction(nameof(Index));
    }
}
