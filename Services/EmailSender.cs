using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace RentalWeb.Emails.Functions.Services;

public class EmailSender
{
    private readonly EmailClient _client;
    private readonly string _fromEmail;
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(EmailClient client, IConfiguration configuration, ILogger<EmailSender> logger)
    {
        _client = client;
        _logger = logger;
        _fromEmail = configuration["CommunicationServicesFromEmail"]
            ?? throw new InvalidOperationException("CommunicationServicesFromEmail is not configured.");
    }

    public async Task SendAsync(string to, string subject, string plainText, string html, CancellationToken cancellationToken)
    {
        var content = new EmailContent(subject)
        {
            PlainText = plainText,
            Html = html
        };

        var recipients = new EmailRecipients(new List<EmailAddress>
        {
            new EmailAddress(to)
        });

        var message = new EmailMessage(_fromEmail, recipients, content);

        await _client.SendAsync(WaitUntil.Completed, message, cancellationToken);
        _logger.LogInformation("Email sent to {Recipient}.", to);
    }
}
