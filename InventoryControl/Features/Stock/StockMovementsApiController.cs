using InventoryControl.Infrastructure.Persistence;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryControl.Features.Stock;

[ApiController]
[Route("api/v1/stock-movements")]
[Authorize(AuthenticationSchemes = "ApiKey")]
[Produces("application/json")]
public class StockMovementsApiController : ControllerBase
{
    private readonly IStockMovementRepository _movementRepo;
    private readonly IProductRepository _productRepo;
    private readonly ISupplierRepository _supplierRepo;
    private readonly AppDbContext _dbContext;

    public StockMovementsApiController(
        IStockMovementRepository movementRepo,
        IProductRepository productRepo,
        ISupplierRepository supplierRepo,
        AppDbContext dbContext)
    {
        _movementRepo = movementRepo;
        _productRepo = productRepo;
        _supplierRepo = supplierRepo;
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<object>>> GetAll(int page = 1, int pageSize = 20)
    {
        var result = await _movementRepo.GetAllAsync(page, pageSize);
        var mapped = new PagedResult<object>(
            result.Items.Select(MapMovement).ToList(),
            result.TotalCount, result.Page, result.PageSize);
        return Ok(mapped);
    }

    [HttpPost("entry")]
    public async Task<ActionResult<object>> CreateEntry([FromBody] StockEntryDto dto)
    {
        var product = await _productRepo.GetByIdAsync(dto.ProductId);
        if (product is null) return NotFound(new { error = "Product not found." });

        if (dto.SupplierId is not null && !await _supplierRepo.ExistsAsync(dto.SupplierId.Value))
            return BadRequest(new { error = "Supplier not found." });

        var movement = new StockMovement
        {
            ProductId = dto.ProductId,
            Type = MovementType.Entry,
            Quantity = dto.Quantity,
            Date = dto.Date ?? DateTime.UtcNow,
            SupplierId = dto.SupplierId,
            UnitCost = dto.UnitCost,
            Notes = dto.Notes
        };

        try
        {
            product.ApplyEntry(dto.Quantity);
        }
        catch (ProductArchivedException ex)
        {
            return Conflict(new { error = "ProductArchived", message = ex.Message, productName = ex.ProductName });
        }

        _dbContext.StockMovements.Add(movement);
        await _dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), null, MapMovement(movement));
    }

    [HttpPost("exit")]
    public async Task<ActionResult<object>> CreateExit([FromBody] StockExitDto dto)
    {
        var product = await _productRepo.GetByIdAsync(dto.ProductId);
        if (product is null) return NotFound(new { error = "Product not found." });

        var movement = new StockMovement
        {
            ProductId = dto.ProductId,
            Type = MovementType.Exit,
            Quantity = dto.Quantity,
            Date = dto.Date ?? DateTime.UtcNow,
            ExitReason = dto.ExitReason,
            Notes = dto.Notes
        };

        try
        {
            product.ApplyExit(dto.Quantity);
        }
        catch (InsufficientStockException ex)
        {
            return BadRequest(new { error = "Insufficient stock.", available = ex.Available, requested = ex.Requested });
        }
        catch (ProductArchivedException ex)
        {
            return Conflict(new { error = "ProductArchived", message = ex.Message, productName = ex.ProductName });
        }

        _dbContext.StockMovements.Add(movement);
        await _dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), null, MapMovement(movement));
    }

    private static object MapMovement(StockMovement m) => new
    {
        m.Id,
        m.ProductId,
        ProductName = m.Product?.Name,
        Type = m.Type.ToString(),
        m.Quantity,
        m.Date,
        m.SupplierId,
        SupplierName = m.Supplier?.Name,
        m.UnitCost,
        ExitReason = m.ExitReason?.ToString(),
        m.Notes
    };
}

public class StockEntryDto
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public DateTime? Date { get; set; }
    public int? SupplierId { get; set; }
    public decimal? UnitCost { get; set; }
    public string? Notes { get; set; }
}

public class StockExitDto
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public DateTime? Date { get; set; }
    public ExitReason? ExitReason { get; set; }
    public string? Notes { get; set; }
}
