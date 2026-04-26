using InventoryControl.Domain.Products.Events;
using InventoryControl.Infrastructure.Email;
using InventoryControl.Infrastructure.Events;

namespace InventoryControl.Features.Notifications.Handlers;

public class EmailOnProductWentBelowMinimum : IDomainEventHandler<ProductWentBelowMinimum>
{
    private readonly IEmailSender _emailSender;
    private readonly ILogger<EmailOnProductWentBelowMinimum> _logger;

    public EmailOnProductWentBelowMinimum(
        IEmailSender emailSender,
        ILogger<EmailOnProductWentBelowMinimum> logger)
    {
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task HandleAsync(ProductWentBelowMinimum @event, CancellationToken ct)
    {
        var subject = $"[Inventory Control] Low stock: {@event.ProductName}";
        var body =
            $"Product \"{@event.ProductName}\" (id={@event.ProductId}) just crossed below minimum stock.\n\n" +
            $"Current stock: {@event.CurrentStock}\n" +
            $"Minimum stock: {@event.MinimumStock}\n" +
            $"Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\n";

        await _emailSender.SendAsync(subject, body, ct);
        _logger.LogInformation(
            "Low-stock alert sent for product {ProductName} (id={ProductId}).",
            @event.ProductName, @event.ProductId);
    }
}
