using ControleEstoque.Models;
using ControleEstoque.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControleEstoque.Controllers.Api;

[ApiController]
[Route("api/v1/suppliers")]
[Authorize(AuthenticationSchemes = "ApiKey")]
[Produces("application/json")]
public class SuppliersApiController : ControllerBase
{
    private readonly ISupplierRepository _repo;

    public SuppliersApiController(ISupplierRepository repo)
    {
        _repo = repo;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<object>>> GetAll(int page = 1, int pageSize = 20)
    {
        var result = await _repo.GetAllAsync(page, pageSize);
        var mapped = new PagedResult<object>(
            result.Items.Select(s => MapSupplier(s)).ToList(),
            result.TotalCount, result.Page, result.PageSize);
        return Ok(mapped);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<object>> GetById(int id)
    {
        var supplier = await _repo.GetByIdAsync(id);
        if (supplier is null) return NotFound(new { error = "Supplier not found." });
        return Ok(MapSupplier(supplier));
    }

    [HttpPost]
    public async Task<ActionResult<object>> Create([FromBody] SupplierDto dto)
    {
        var supplier = new Supplier
        {
            Name = dto.Name,
            Cnpj = dto.Cnpj,
            Phone = dto.Phone,
            Email = dto.Email
        };

        await _repo.AddAsync(supplier);
        return CreatedAtAction(nameof(GetById), new { id = supplier.Id }, MapSupplier(supplier));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<object>> Update(int id, [FromBody] SupplierDto dto)
    {
        var supplier = await _repo.GetByIdAsync(id);
        if (supplier is null) return NotFound(new { error = "Supplier not found." });

        supplier.Name = dto.Name;
        supplier.Cnpj = dto.Cnpj;
        supplier.Phone = dto.Phone;
        supplier.Email = dto.Email;

        await _repo.UpdateAsync(supplier);
        return Ok(MapSupplier(supplier));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!await _repo.ExistsAsync(id))
            return NotFound(new { error = "Supplier not found." });

        await _repo.DeleteAsync(id);
        return NoContent();
    }

    private static object MapSupplier(Supplier s) => new
    {
        s.Id,
        s.Name,
        s.Cnpj,
        s.Phone,
        s.Email,
        ProductCount = s.Products?.Count ?? 0
    };
}

public class SupplierDto
{
    public string Name { get; set; } = string.Empty;
    public string? Cnpj { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}
