using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RentalWeb.Emails.Functions.Models;
using RentalWeb.Emails.Functions.Services;

namespace RentalWeb.Emails.Functions.Functions;

public class EmailProcessor
{
    private readonly EmailSender _emailSender;
    private readonly ILogger<EmailProcessor> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public EmailProcessor(EmailSender emailSender, ILogger<EmailProcessor> logger)
    {
        _emailSender = emailSender;
        _logger = logger;
    }

    [Function("EmailProcessor")]
    public async Task Run(
        [ServiceBusTrigger("%ServiceBusQueueName%", Connection = "ServiceBusConnectionString")] string message,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        EmailQueueMessage? envelope;

        try
        {
            envelope = JsonSerializer.Deserialize<EmailQueueMessage>(message, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize email message.");
            return;
        }

        if (envelope is null)
        {
            _logger.LogWarning("Empty email message received.");
            return;
        }

        switch (envelope.EventType)
        {
            case "UserRegistered":
                await HandleUserRegistered(envelope.Data, cancellationToken);
                break;
            case "ListingCreated":
                await HandleListingCreated(envelope.Data, cancellationToken);
                break;
            case "ApplicationCreated":
                await HandleApplicationCreated(envelope.Data, cancellationToken);
                break;
            case "ViewingApproved":
                await HandleViewingApproved(envelope.Data, cancellationToken);
                break;
            case "ListingApproved":
                await HandleListingApproved(envelope.Data, cancellationToken);
                break;
            default:
                _logger.LogWarning("Unknown email event type: {EventType}", envelope.EventType);
                break;
        }
    }

    private async Task HandleUserRegistered(JsonElement data, CancellationToken cancellationToken)
    {
        var email = GetString(data, "email");
        var role = GetString(data, "role");

        if (string.IsNullOrWhiteSpace(email))
        {
            _logger.LogWarning("UserRegistered missing email.");
            return;
        }

        var subject = "Welcome to RentalWeb";
        var plain = $"Thanks for signing up. Your role is {role}.";
        var html = BuildHtml(plain);

        await _emailSender.SendAsync(email, subject, plain, html, cancellationToken);
    }

    private async Task HandleListingCreated(JsonElement data, CancellationToken cancellationToken)
    {
        var ownerEmail = GetString(data, "ownerEmail");
        var title = GetString(data, "title");
        var location = GetString(data, "location");

        if (string.IsNullOrWhiteSpace(ownerEmail))
        {
            _logger.LogWarning("ListingCreated missing ownerEmail.");
            return;
        }

        var subject = $"Listing created: {title}";
        var plain = $"Your listing \"{title}\" in {location} is live.";
        var html = BuildHtml(plain);

        await _emailSender.SendAsync(ownerEmail, subject, plain, html, cancellationToken);
    }

    private async Task HandleApplicationCreated(JsonElement data, CancellationToken cancellationToken)
    {
        var applicantEmail = GetString(data, "applicantEmail");
        var ownerEmail = GetString(data, "ownerEmail");
        var title = GetString(data, "listingTitle");

        if (!string.IsNullOrWhiteSpace(applicantEmail))
        {
            var subject = $"Application received: {title}";
            var plain = $"We received your application for \"{title}\". The lister will follow up with next steps.";
            await _emailSender.SendAsync(applicantEmail, subject, plain, BuildHtml(plain), cancellationToken);
        }
        else
        {
            _logger.LogWarning("ApplicationCreated missing applicantEmail.");
        }

        if (!string.IsNullOrWhiteSpace(ownerEmail))
        {
            var subject = $"New application: {title}";
            var plain = $"You received a new application for \"{title}\".";
            await _emailSender.SendAsync(ownerEmail, subject, plain, BuildHtml(plain), cancellationToken);
        }
        else
        {
            _logger.LogWarning("ApplicationCreated missing ownerEmail.");
        }
    }

    private async Task HandleViewingApproved(JsonElement data, CancellationToken cancellationToken)
    {
        var applicantEmail = GetString(data, "applicantEmail");
        var title = GetString(data, "listingTitle");

        if (string.IsNullOrWhiteSpace(applicantEmail))
        {
            _logger.LogWarning("ViewingApproved missing applicantEmail.");
            return;
        }

        var subject = $"Viewing approved: {title}";
        var plain = $"Your viewing request for \"{title}\" has been approved. The lister will contact you with details.";
        await _emailSender.SendAsync(applicantEmail, subject, plain, BuildHtml(plain), cancellationToken);
    }

    private async Task HandleListingApproved(JsonElement data, CancellationToken cancellationToken)
    {
        var applicantEmail = GetString(data, "applicantEmail");
        var title = GetString(data, "listingTitle");

        if (string.IsNullOrWhiteSpace(applicantEmail))
        {
            _logger.LogWarning("ListingApproved missing applicantEmail.");
            return;
        }

        var subject = $"Application approved: {title}";
        var plain = $"Your application for \"{title}\" has been approved.";
        await _emailSender.SendAsync(applicantEmail, subject, plain, BuildHtml(plain), cancellationToken);
    }

    private static string GetString(JsonElement data, string name)
    {
        if (data.ValueKind == JsonValueKind.Object &&
            data.TryGetProperty(name, out var value) &&
            value.ValueKind == JsonValueKind.String)
        {
            return value.GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    private static string BuildHtml(string plain)
    {
        var encoded = WebUtility.HtmlEncode(plain).Replace("\n", "<br/>");
        return $"<p>{encoded}</p>";
    }
}
