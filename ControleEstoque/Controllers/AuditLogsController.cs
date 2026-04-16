using ControleEstoque.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleEstoque.Controllers;

[Authorize(Roles = "Admin")]
public class AuditLogsController : Controller
{
    private readonly AppDbContext _context;

    public AuditLogsController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var logs = await _context.AuditLogs
            .OrderByDescending(a => a.Timestamp)
            .Take(200)
            .ToListAsync();

        return View(logs);
    }
}
