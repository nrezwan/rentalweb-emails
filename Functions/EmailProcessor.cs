using System.Net;
using System.Text;
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
        var intro = "Thanks for signing up. Your account is ready.";
        var details = new Dictionary<string, string>
        {
            ["Role"] = string.IsNullOrWhiteSpace(role) ? "Applicant" : role
        };
        var footer = "Visit RentalWeb to start exploring listings or managing your rentals.";
        var plain = BuildPlainText(intro, details, footer);
        var html = BuildHtml(subject, intro, details, footer);

        await _emailSender.SendAsync(email, subject, plain, html, cancellationToken);
    }

    private async Task HandleListingCreated(JsonElement data, CancellationToken cancellationToken)
    {
        var ownerEmail = GetString(data, "ownerEmail");
        var title = GetString(data, "title");
        var location = GetString(data, "location");
        var price = GetString(data, "price");

        if (string.IsNullOrWhiteSpace(ownerEmail))
        {
            _logger.LogWarning("ListingCreated missing ownerEmail.");
            return;
        }

        var subject = $"Listing created: {title}";
        var intro = "Your listing is live and ready for applicants.";
        var details = new Dictionary<string, string>
        {
            ["Title"] = title,
            ["Location"] = location,
            ["Price"] = price
        };
        var footer = "We'll notify you as soon as someone applies.";
        var plain = BuildPlainText(intro, details, footer);
        var html = BuildHtml(subject, intro, details, footer);

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
            var intro = "We received your application. The lister will follow up with next steps.";
            var details = new Dictionary<string, string>
            {
                ["Listing"] = title
            };
            var footer = "You can keep an eye on your applications in RentalWeb.";
            var plain = BuildPlainText(intro, details, footer);
            await _emailSender.SendAsync(applicantEmail, subject, plain, BuildHtml(subject, intro, details, footer), cancellationToken);
        }
        else
        {
            _logger.LogWarning("ApplicationCreated missing applicantEmail.");
        }

        if (!string.IsNullOrWhiteSpace(ownerEmail))
        {
            var subject = $"New application: {title}";
            var intro = "You received a new application for your listing.";
            var details = new Dictionary<string, string>
            {
                ["Listing"] = title
            };
            var footer = "Review the application and approve a viewing when ready.";
            var plain = BuildPlainText(intro, details, footer);
            await _emailSender.SendAsync(ownerEmail, subject, plain, BuildHtml(subject, intro, details, footer), cancellationToken);
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
        var intro = "Your viewing request has been approved.";
        var details = new Dictionary<string, string>
        {
            ["Listing"] = title
        };
        var footer = "The lister will contact you shortly with timing details.";
        var plain = BuildPlainText(intro, details, footer);
        await _emailSender.SendAsync(applicantEmail, subject, plain, BuildHtml(subject, intro, details, footer), cancellationToken);
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
        var intro = "Good news. Your application has been approved.";
        var details = new Dictionary<string, string>
        {
            ["Listing"] = title
        };
        var footer = "You can follow up with the lister in RentalWeb.";
        var plain = BuildPlainText(intro, details, footer);
        await _emailSender.SendAsync(applicantEmail, subject, plain, BuildHtml(subject, intro, details, footer), cancellationToken);
    }

    private static string GetString(JsonElement data, string name)
    {
        if (data.ValueKind == JsonValueKind.Object &&
            data.TryGetProperty(name, out var value) &&
            value.ValueKind == JsonValueKind.String)
        {
            return value.GetString() ?? string.Empty;
        }

        if (data.ValueKind == JsonValueKind.Object &&
            data.TryGetProperty(name, out var numberValue) &&
            numberValue.ValueKind == JsonValueKind.Number)
        {
            return numberValue.ToString();
        }

        return string.Empty;
    }

    private static string BuildPlainText(string intro, IDictionary<string, string> details, string footer)
    {
        var sb = new StringBuilder();
        sb.AppendLine(intro);
        if (details.Count > 0)
        {
            sb.AppendLine();
            foreach (var item in details)
            {
                if (!string.IsNullOrWhiteSpace(item.Value))
                {
                    sb.AppendLine($"{item.Key}: {item.Value}");
                }
            }
        }
        if (!string.IsNullOrWhiteSpace(footer))
        {
            sb.AppendLine();
            sb.AppendLine(footer);
        }

        return sb.ToString().Trim();
    }

    private static string BuildHtml(string heading, string intro, IDictionary<string, string> details, string footer)
    {
        var headingEscaped = WebUtility.HtmlEncode(heading);
        var introEscaped = WebUtility.HtmlEncode(intro);
        var footerEscaped = WebUtility.HtmlEncode(footer);
        var detailRows = new StringBuilder();

        foreach (var item in details)
        {
            if (string.IsNullOrWhiteSpace(item.Value))
            {
                continue;
            }

            detailRows.Append(
                $"<tr><td style=\"padding:8px 12px;color:#64748b;font-size:13px;border-bottom:1px solid #e2e8f0;\">{WebUtility.HtmlEncode(item.Key)}</td>" +
                $"<td style=\"padding:8px 12px;color:#0f172a;font-size:13px;border-bottom:1px solid #e2e8f0;\">{WebUtility.HtmlEncode(item.Value)}</td></tr>");
        }

        var detailsHtml = detailRows.Length == 0
            ? string.Empty
            : $"<table role=\"presentation\" style=\"width:100%;border-collapse:collapse;margin-top:12px;border:1px solid #e2e8f0;\">{detailRows}</table>";

        return $@"
<div style=""margin:0;padding:24px;background:#f8fafc;font-family:Arial,sans-serif;"">
  <div style=""max-width:560px;margin:0 auto;background:#ffffff;border-radius:12px;border:1px solid #e2e8f0;overflow:hidden;"">
    <div style=""background:#0f172a;color:#ffffff;padding:20px 24px;font-size:20px;font-weight:600;"">{headingEscaped}</div>
    <div style=""padding:24px;color:#0f172a;font-size:14px;line-height:1.6;"">
      <p style=""margin:0 0 12px 0;"">{introEscaped}</p>
      {detailsHtml}
      <p style=""margin:16px 0 0 0;color:#475569;"">{footerEscaped}</p>
    </div>
    <div style=""padding:16px 24px;background:#f1f5f9;color:#64748b;font-size:12px;"">
      RentalWeb notifications
    </div>
  </div>
</div>";
    }
}
