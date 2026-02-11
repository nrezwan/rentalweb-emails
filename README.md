# RentalWeb Emails

Azure Functions app that consumes Service Bus email events and sends emails through Azure Communication Services.

## Configuration
Set these values using environment variables or `local.settings.json`:

- `ServiceBusConnectionString`
- `ServiceBusQueueName`
- `CommunicationServicesConnectionString`
- `CommunicationServicesFromEmail`

## Local Run
1. Update `local.settings.json` with your settings.
2. Run `func start` (recommended) or `dotnet run`.

## Event Schema
Each Service Bus message is JSON in the form:

```json
{
  "eventType": "ApplicationCreated",
  "data": {},
  "enqueuedAt": "2026-02-11T00:00:00Z"
}
```
