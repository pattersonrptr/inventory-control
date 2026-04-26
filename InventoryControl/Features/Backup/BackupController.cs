using System.Security.Claims;
using InventoryControl.Infrastructure.Persistence;
using InventoryControl.Infrastructure.Backup;
using InventoryControl.Infrastructure;
using InventoryControl.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryControl.Features.Backup;

[Authorize(Roles = "Admin")]
public class BackupController : Controller
{
    private readonly IDatabaseBackupService _backupService;
    private readonly IOffsiteBackupService _offsiteBackupService;
    private readonly ILogger<BackupController> _logger;
    private readonly AppDbContext _db;

    public BackupController(
        IDatabaseBackupService backupService,
        IOffsiteBackupService offsiteBackupService,
        ILogger<BackupController> logger,
        AppDbContext db)
    {
        _backupService = backupService;
        _offsiteBackupService = offsiteBackupService;
        _logger = logger;
        _db = db;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var vm = new BackupViewModel
        {
            IsRcloneConfigured = await _offsiteBackupService.IsConfiguredAsync(cancellationToken),
            ConfiguredRemotes = await _offsiteBackupService.GetConfiguredRemotesAsync(cancellationToken),
            RemotePath = _offsiteBackupService.RemotePath
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Download(CancellationToken cancellationToken)
    {
        var userName = User.Identity?.Name ?? "unknown";
        _logger.LogInformation("Backup download requested by user '{User}'.", userName);

        try
        {
            var (stream, fileName) = await _backupService.CreateBackupAsync(cancellationToken);

            await WriteAuditAsync("BackupDownload", fileName, cancellationToken);

            _logger.LogInformation("Backup download completed: {FileName}.", fileName);

            var contentType = fileName.EndsWith(".db", StringComparison.OrdinalIgnoreCase)
                ? "application/octet-stream"
                : "application/sql";

            return File(stream, contentType, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup download failed for user '{User}'.", userName);
            TempData["Error"] = "Falha ao gerar o backup. Por favor, tente novamente.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadToCloud(CancellationToken cancellationToken)
    {
        var userName = User.Identity?.Name ?? "unknown";
        _logger.LogInformation("Cloud backup upload requested by user '{User}'.", userName);

        try
        {
            var (stream, fileName) = await _backupService.CreateBackupAsync(cancellationToken);
            var (success, message) = await _offsiteBackupService.UploadAsync(stream, fileName, cancellationToken);

            if (!success)
            {
                _logger.LogError("Cloud backup upload failed for user '{User}': {Message}.", userName, message);
                TempData["Error"] = $"Falha no upload: {message}";
                return RedirectToAction(nameof(Index));
            }

            await WriteAuditAsync("BackupUploadCloud", fileName, cancellationToken);

            _logger.LogInformation("Cloud backup upload completed: {FileName}.", fileName);
            TempData["Success"] = message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cloud backup upload failed for user '{User}'.", userName);
            TempData["Error"] = "Falha no upload. Por favor, tente novamente.";
        }

        return RedirectToAction(nameof(Index));
    }

    // -------------------------------------------------------------------------

    private async Task WriteAuditAsync(string action, string fileName, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
        var userName = User.Identity?.Name ?? "unknown";
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            UserName = userName,
            Action = action,
            EntityName = "Database",
            EntityId = null,
            Timestamp = DateTime.UtcNow,
            OldValues = null,
            NewValues = $"{{\"FileName\":\"{fileName}\",\"RequestedAt\":\"{DateTime.UtcNow:O}\"}}"
        });
        await _db.SaveChangesAsync(ct);
    }
}

