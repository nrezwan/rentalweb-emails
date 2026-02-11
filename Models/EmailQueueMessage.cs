using System.Text.Json;

namespace RentalWeb.Emails.Functions.Models;

public record EmailQueueMessage(string EventType, JsonElement Data, DateTimeOffset EnqueuedAt);
