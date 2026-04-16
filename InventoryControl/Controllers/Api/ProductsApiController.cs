using InventoryControl.Models;
using InventoryControl.Repositories.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryControl.Controllers.Api;

[ApiController]
[Route("api/v1/products")]
[Authorize(AuthenticationSchemes = "ApiKey")]
[Produces("application/json")]
public class ProductsApiController : ControllerBase
{
    private readonly IProductRepository _repo;

    public ProductsApiController(IProductRepository repo)
    {
        _repo = repo;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<object>>> GetAll(int page = 1, int pageSize = 20)
    {
        var result = await _repo.GetAllAsync(page, pageSize);
        var mapped = new PagedResult<object>(
            result.Items.Select(p => MapProduct(p)).ToList(),
            result.TotalCount, result.Page, result.PageSize);
        return Ok(mapped);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<object>> GetById(int id)
    {
        var product = await _repo.GetByIdAsync(id);
        if (product is null) return NotFound(new { error = "Product not found." });
        return Ok(MapProduct(product));
    }

    [HttpGet("below-minimum")]
    public async Task<ActionResult<IEnumerable<object>>> GetBelowMinimum()
    {
        var products = await _repo.GetBelowMinimumAsync();
        return Ok(products.Select(MapProduct));
    }

    [HttpPost]
    public async Task<ActionResult<object>> Create([FromBody] ProductCreateDto dto)
    {
        var product = new Product
        {
            Name = dto.Name,
            Description = dto.Description,
            CostPrice = dto.CostPrice,
            SellingPrice = dto.SellingPrice,
            MinimumStock = dto.MinimumStock,
            Sku = dto.Sku,
            CategoryId = dto.CategoryId,
            SupplierId = dto.SupplierId
        };

        await _repo.AddAsync(product);
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, MapProduct(product));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<object>> Update(int id, [FromBody] ProductUpdateDto dto)
    {
        var product = await _repo.GetByIdAsync(id);
        if (product is null) return NotFound(new { error = "Product not found." });

        product.Name = dto.Name;
        product.Description = dto.Description;
        product.CostPrice = dto.CostPrice;
        product.SellingPrice = dto.SellingPrice;
        product.MinimumStock = dto.MinimumStock;
        product.Sku = dto.Sku;
        product.CategoryId = dto.CategoryId;
        product.SupplierId = dto.SupplierId;

        await _repo.UpdateAsync(product);
        return Ok(MapProduct(product));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!await _repo.ExistsAsync(id))
            return NotFound(new { error = "Product not found." });

        await _repo.DeleteAsync(id);
        return NoContent();
    }

    [HttpPatch("{id}/stock")]
    public async Task<IActionResult> UpdateStock(int id, [FromBody] StockUpdateDto dto)
    {
        if (!await _repo.ExistsAsync(id))
            return NotFound(new { error = "Product not found." });

        await _repo.UpdateStockAsync(id, dto.Quantity);
        return Ok(new { message = "Stock updated.", productId = id, newQuantity = dto.Quantity });
    }

    private static object MapProduct(Product p) => new
    {
        p.Id,
        p.Name,
        p.Description,
        p.CostPrice,
        p.SellingPrice,
        p.CurrentStock,
        p.MinimumStock,
        p.Sku,
        p.Brand,
        ImagePath = p.PrimaryImagePath,
        p.CategoryId,
        CategoryName = p.Category?.Name,
        p.SupplierId,
        SupplierName = p.Supplier?.Name,
        p.ExternalId,
        p.ExternalIdSource,
        IsBelowMinimumStock = p.IsBelowMinimumStock
    };
}

public class ProductCreateDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal CostPrice { get; set; }
    public decimal SellingPrice { get; set; }
    public int MinimumStock { get; set; }
    public string? Sku { get; set; }
    public int CategoryId { get; set; }
    public int SupplierId { get; set; }
}

public class ProductUpdateDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal CostPrice { get; set; }
    public decimal SellingPrice { get; set; }
    public int MinimumStock { get; set; }
    public string? Sku { get; set; }
    public int CategoryId { get; set; }
    public int SupplierId { get; set; }
}

public class StockUpdateDto
{
    public int Quantity { get; set; }
}
