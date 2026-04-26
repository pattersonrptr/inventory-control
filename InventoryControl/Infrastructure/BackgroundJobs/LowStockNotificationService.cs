using System.Text;

using InventoryControl.Infrastructure.Email;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InventoryControl.Infrastructure.BackgroundJobs;

/// <summary>
/// Background service that periodically checks for products below minimum stock
/// and sends email notifications via SMTP when low-stock items are found.
/// Configurable via the "EmailNotifications" section in appsettings.json.
/// </summary>
public class LowStockNotificationService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LowStockNotificationService> _logger;
    private readonly IConfiguration _configuration;
    private readonly TimeSpan _interval;

    public LowStockNotificationService(
        IServiceScopeFactory scopeFactory,
        ILogger<LowStockNotificationService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _configuration = configuration;

        var hours = configuration.GetValue<int?>("EmailNotifications:CheckIntervalHours") ?? 24;
        _interval = TimeSpan.FromHours(hours);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "LowStockNotificationService started. Check interval: {Interval}.", _interval);

        // Delay the first run so the app finishes starting up.
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await CheckAndNotifyAsync(stoppingToken);
            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task CheckAndNotifyAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var productRepo = scope.ServiceProvider.GetRequiredService<IProductRepository>();

            var lowStockProducts = (await productRepo.GetBelowMinimumAsync()).ToList();

            if (lowStockProducts.Count == 0)
            {
                _logger.LogInformation("LowStockNotificationService: no products below minimum stock.");
                return;
            }

            _logger.LogWarning(
                "LowStockNotificationService: {Count} product(s) below minimum stock.",
                lowStockProducts.Count);

            var body = new StringBuilder();
            body.AppendLine("The following products are below minimum stock levels:");
            body.AppendLine();
            body.AppendLine("| Product | Current Stock | Minimum Stock | Deficit |");
            body.AppendLine("|---------|--------------|---------------|---------|");

            foreach (var product in lowStockProducts)
            {
                var deficit = product.MinimumStock - product.CurrentStock;
                body.AppendLine(
                    $"| {product.Name} | {product.CurrentStock} | {product.MinimumStock} | {deficit} |");
            }

            body.AppendLine();
            body.AppendLine($"Total products below minimum: {lowStockProducts.Count}");
            body.AppendLine($"Check time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");

            await using var innerScope = _scopeFactory.CreateAsyncScope();
            var emailSender = innerScope.ServiceProvider.GetRequiredService<IEmailSender>();
            await emailSender.SendAsync(
                $"[Inventory Control] Low Stock Alert — {lowStockProducts.Count} product(s)",
                body.ToString(),
                stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LowStockNotificationService: error during low stock check.");
        }
    }
}
