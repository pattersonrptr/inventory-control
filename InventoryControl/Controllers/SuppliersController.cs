using InventoryControl.Models;
using InventoryControl.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryControl.Controllers;

public class SuppliersController : Controller
{
    private readonly ISupplierRepository _repository;

    public SuppliersController(ISupplierRepository repository)
    {
        _repository = repository;
    }

    public async Task<IActionResult> Index(int page = 1, int pageSize = 20)
        => View(await _repository.GetAllAsync(page, pageSize));

    public IActionResult Create() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Supplier supplier)
    {
        if (!ModelState.IsValid) return View(supplier);

        await _repository.AddAsync(supplier);
        TempData["Success"] = "Fornecedor criado com sucesso!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateInline([FromBody] Supplier supplier)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });

        await _repository.AddAsync(supplier);
        return StatusCode(201, new { id = supplier.Id, name = supplier.Name });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var supplier = await _repository.GetByIdAsync(id);
        if (supplier is null) return NotFound();
        return View(supplier);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Supplier supplier)
    {
        if (id != supplier.Id) return BadRequest();
        if (!ModelState.IsValid) return View(supplier);

        await _repository.UpdateAsync(supplier);
        TempData["Success"] = "Fornecedor atualizado com sucesso!";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var supplier = await _repository.GetByIdAsync(id);
        if (supplier is null) return NotFound();
        return View(supplier);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        try
        {
            await _repository.DeleteAsync(id);
            TempData["Success"] = "Fornecedor excluído com sucesso!";
        }
        catch (DbUpdateException)
        {
            TempData["Error"] = "Não é possível excluir este fornecedor porque existem produtos vinculados a ele.";
        }
        return RedirectToAction(nameof(Index));
    }
}
