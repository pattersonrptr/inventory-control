namespace InventoryControl.Infrastructure.Email;

public interface IEmailSender
{
    Task<bool> SendAsync(string subject, string body, CancellationToken ct = default);
}
