using System.Text.Json;
using InventoryControl.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace InventoryControl.Data;

public class AuditInterceptor : SaveChangesInterceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditInterceptor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not AppDbContext context)
            return base.SavingChangesAsync(eventData, result, cancellationToken);

        var user = _httpContextAccessor.HttpContext?.User;
        var userId = user?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "system";
        var userName = user?.Identity?.Name ?? "system";

        var entries = context.ChangeTracker.Entries()
            .Where(e => e.Entity is not AuditLog
                && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        foreach (var entry in entries)
        {
            var entityName = entry.Entity.GetType().Name;
            var entityId = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "Id")?.CurrentValue?.ToString();

            var action = entry.State switch
            {
                EntityState.Added => "Created",
                EntityState.Modified => "Updated",
                EntityState.Deleted => "Deleted",
                _ => "Unknown"
            };

            string? oldValues = null;
            string? newValues = null;

            if (entry.State == EntityState.Modified)
            {
                var changed = entry.Properties
                    .Where(p => p.IsModified)
                    .ToDictionary(p => p.Metadata.Name, p => p.OriginalValue?.ToString());
                oldValues = JsonSerializer.Serialize(changed);

                var current = entry.Properties
                    .Where(p => p.IsModified)
                    .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue?.ToString());
                newValues = JsonSerializer.Serialize(current);
            }
            else if (entry.State == EntityState.Added)
            {
                var values = entry.Properties
                    .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue?.ToString());
                newValues = JsonSerializer.Serialize(values);
            }
            else if (entry.State == EntityState.Deleted)
            {
                var values = entry.Properties
                    .ToDictionary(p => p.Metadata.Name, p => p.OriginalValue?.ToString());
                oldValues = JsonSerializer.Serialize(values);
            }

            context.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                UserName = userName,
                Action = action,
                EntityName = entityName,
                EntityId = entityId,
                Timestamp = DateTime.Now,
                OldValues = Truncate(oldValues, 4000),
                NewValues = Truncate(newValues, 4000)
            });
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static string? Truncate(string? value, int maxLength)
        => value?.Length > maxLength ? value[..maxLength] : value;
}
