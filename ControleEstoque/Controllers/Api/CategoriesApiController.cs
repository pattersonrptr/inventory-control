using ControleEstoque.Models;
using ControleEstoque.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControleEstoque.Controllers.Api;

[ApiController]
[Route("api/v1/categories")]
[Authorize(AuthenticationSchemes = "ApiKey")]
[Produces("application/json")]
public class CategoriesApiController : ControllerBase
{
    private readonly ICategoryRepository _repo;

    public CategoriesApiController(ICategoryRepository repo)
    {
        _repo = repo;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<object>>> GetAll(int page = 1, int pageSize = 20)
    {
        var result = await _repo.GetAllAsync(page, pageSize);
        var mapped = new PagedResult<object>(
            result.Items.Select(c => MapCategory(c)).ToList(),
            result.TotalCount, result.Page, result.PageSize);
        return Ok(mapped);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<object>> GetById(int id)
    {
        var category = await _repo.GetByIdAsync(id);
        if (category is null) return NotFound(new { error = "Category not found." });
        return Ok(MapCategory(category));
    }

    [HttpPost]
    public async Task<ActionResult<object>> Create([FromBody] CategoryDto dto)
    {
        var category = new Category
        {
            Name = dto.Name,
            Description = dto.Description
        };

        await _repo.AddAsync(category);
        return CreatedAtAction(nameof(GetById), new { id = category.Id }, MapCategory(category));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<object>> Update(int id, [FromBody] CategoryDto dto)
    {
        var category = await _repo.GetByIdAsync(id);
        if (category is null) return NotFound(new { error = "Category not found." });

        category.Name = dto.Name;
        category.Description = dto.Description;

        await _repo.UpdateAsync(category);
        return Ok(MapCategory(category));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!await _repo.ExistsAsync(id))
            return NotFound(new { error = "Category not found." });

        await _repo.DeleteAsync(id);
        return NoContent();
    }

    private static object MapCategory(Category c) => new
    {
        c.Id,
        c.Name,
        c.Description,
        c.ExternalId,
        c.ExternalIdSource,
        ProductCount = c.Products?.Count ?? 0
    };
}

public class CategoryDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
