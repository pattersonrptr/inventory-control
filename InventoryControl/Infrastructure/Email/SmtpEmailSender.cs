using System.Net;
using System.Net.Mail;

namespace InventoryControl.Infrastructure.Email;

public class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> SendAsync(string subject, string body, CancellationToken ct = default)
    {
        var smtpHost = _configuration["EmailNotifications:SmtpHost"];
        var smtpPort = _configuration.GetValue<int?>("EmailNotifications:SmtpPort") ?? 587;
        var smtpUser = _configuration["EmailNotifications:SmtpUser"];
        var smtpPassword = _configuration["EmailNotifications:SmtpPassword"];
        var fromEmail = _configuration["EmailNotifications:FromEmail"];
        var toEmail = _configuration["EmailNotifications:ToEmail"];
        var enableSsl = _configuration.GetValue<bool?>("EmailNotifications:EnableSsl") ?? true;

        if (string.IsNullOrWhiteSpace(smtpHost) || string.IsNullOrWhiteSpace(toEmail))
        {
            _logger.LogWarning("SmtpEmailSender: SMTP not configured. Skipping email \"{Subject}\".", subject);
            return false;
        }

        using var client = new SmtpClient(smtpHost, smtpPort)
        {
            EnableSsl = enableSsl,
            Credentials = !string.IsNullOrWhiteSpace(smtpUser)
                ? new NetworkCredential(smtpUser, smtpPassword)
                : null
        };

        using var message = new MailMessage(
            from: fromEmail ?? smtpUser ?? "noreply@inventory.local",
            to: toEmail,
            subject: subject,
            body: body);

        await client.SendMailAsync(message, ct);
        _logger.LogInformation("SmtpEmailSender: email \"{Subject}\" sent to {To}.", subject, toEmail);
        return true;
    }
}
