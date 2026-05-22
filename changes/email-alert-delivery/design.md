# Email Alert Delivery Design

## Current Architecture Context

MarketAgent already has:

- persisted `AlertEvents`
- `DeliveryStatus = InternalOnly`
- `POST /api/alerts/evaluate`
- `GET /api/alerts`
- manual system cycle endpoint

The email delivery feature should use persisted alert events as its source. It should not re-run alert rules or modify alert evaluation logic.

## API Design

Add:

```text
POST /api/alerts/deliver/email
```

Optional query/body inputs:

- `limit`
- `retryFailed`

V1 recommendation:

- Use query parameters to stay consistent with existing minimal API endpoints.

Example:

```text
POST /api/alerts/deliver/email?limit=25
POST /api/alerts/deliver/email?limit=25&retryFailed=true
```

Default behavior:

- `limit`: safe default such as `25`.
- `retryFailed`: `false`.

## Application Models

Add:

```csharp
public sealed record EmailAlertDeliveryRequest(
    int? Limit,
    bool RetryFailed);
```

Add:

```csharp
public sealed record EmailAlertDeliveryResult(
    DateTime StartedAtUtc,
    DateTime FinishedAtUtc,
    int AttemptedCount,
    int DeliveredCount,
    int FailedCount,
    int SkippedCount,
    IReadOnlyCollection<EmailAlertDeliveryFailure> Failures);
```

Add:

```csharp
public sealed record EmailAlertDeliveryFailure(
    Guid AlertEventId,
    string Symbol,
    string Message);
```

Add delivery statuses:

```csharp
public static class AlertDeliveryStatuses
{
    public const string InternalOnly = "InternalOnly";
    public const string Delivered = "Delivered";
    public const string DeliveryFailed = "DeliveryFailed";
}
```

## Repository Changes

Extend `IAlertEventRepository` with delivery-focused methods:

```csharp
Task<IReadOnlyCollection<AlertEventItem>> GetPendingDeliveryAsync(
    int limit,
    bool includeFailed,
    CancellationToken cancellationToken = default);

Task UpdateDeliveryStatusAsync(
    IReadOnlyCollection<Guid> alertEventIds,
    string deliveryStatus,
    CancellationToken cancellationToken = default);
```

Rules:

- `GetPendingDeliveryAsync` should select `InternalOnly` alerts by default.
- If `includeFailed` is true, include `DeliveryFailed`.
- Never include `Delivered`.
- Sort oldest first so alerts deliver in creation order.
- Limit results.

No schema changes are required for V1.

## Email Options

Add options:

```csharp
public sealed class EmailDeliveryOptions
{
    public const string SectionName = "EmailDelivery";

    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public string? SmtpUser { get; set; }
    public string? SmtpPassword { get; set; }
    public string? FromEmail { get; set; }
    public string FromName { get; set; } = "MarketAgent";
    public string? ToEmail { get; set; }
    public bool EnableSsl { get; set; } = true;
}
```

Validation:

- Require `SmtpHost`.
- Require `FromEmail`.
- Require `ToEmail`.
- Require password/user only when SMTP provider requires authentication.
- Fail safely with a structured response and logs if configuration is incomplete.

## Service Design

Add:

```csharp
public interface IEmailAlertDeliveryService
{
    Task<EmailAlertDeliveryResult> DeliverAsync(
        EmailAlertDeliveryRequest request,
        CancellationToken cancellationToken = default);
}
```

Implementation:

```csharp
public sealed class EmailAlertDeliveryService : IEmailAlertDeliveryService
{
}
```

Responsibilities:

1. Load pending alerts.
2. If none, return zero-count success.
3. Build email content.
4. Send email through SMTP sender.
5. On success, update selected alerts to `Delivered`.
6. On failure, update selected alerts to `DeliveryFailed`.
7. Return structured result.

## SMTP Sender

Add infrastructure abstraction:

```csharp
public interface IEmailSender
{
    Task SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default);
}
```

Application can own the interface, Infrastructure can implement it with MailKit.

Model:

```csharp
public sealed record EmailMessage(
    string FromEmail,
    string FromName,
    string ToEmail,
    string Subject,
    string HtmlBody);
```

Infrastructure implementation:

```text
MailKitEmailSender
```

Use:

- `MimeMessage`
- `BodyBuilder`
- `MailKit.Net.Smtp.SmtpClient`

## Email Content Builder

Add a small pure builder:

```csharp
public static class AlertEmailTemplateBuilder
{
    public static string BuildHtml(IReadOnlyCollection<AlertEventItem> alerts);
}
```

HTML content:

- header: `MarketAgent Alerts`
- summary count
- compact table
- one row per alert

Columns:

- created UTC
- symbol
- setup
- score
- confidence
- price
- title
- message
- reason summary

Reason summary:

- Parse `ReasonJson` if safe.
- Extract concise fields if available.
- If parsing fails, omit or show a short fallback.
- Do not include huge JSON blobs verbatim.

## Delivery Semantics

V1 sends one email per batch, not one email per alert.

If SMTP send succeeds:

- mark all attempted alerts as `Delivered`.

If SMTP send fails:

- mark all attempted alerts as `DeliveryFailed`.
- return one failure summary per attempted alert or a single shared failure message.

Reasoning:

- SMTP send is batch-level in V1.
- Per-alert delivery tracking would require a delivery attempts model or provider-specific feedback.

## Idempotency and Duplicate Send Protection

The main duplicate-send guard is `DeliveryStatus`.

Flow:

1. Query only `InternalOnly` alerts by default.
2. Send email.
3. Update statuses to `Delivered`.

Known race:

- Two manual delivery calls at the same time could query the same pending alerts before either updates status.

V1 mitigation:

- Add an in-process `SemaphoreSlim` in `EmailAlertDeliveryService`.
- Reject overlapping delivery runs safely or return a structured failure.

V2 mitigation:

- Add claim/lease status such as `DeliveryInProgress`.

## Error Handling

- Missing SMTP config should return `FailedCount = 0`, `SkippedCount = pendingCount`, with a clear failure message.
- SMTP failure should log exception details and mark attempted alerts `DeliveryFailed`.
- Repository update failure after SMTP success is risky; log prominently and return failure because duplicate sends may occur later.

## API Mapping

Suggested minimal API:

```csharp
app.MapPost(
    "/api/alerts/deliver/email",
    async (
        IEmailAlertDeliveryService emailAlertDeliveryService,
        int? limit,
        bool? retryFailed,
        CancellationToken cancellationToken) =>
    {
        var result = await emailAlertDeliveryService.DeliverAsync(
            new EmailAlertDeliveryRequest(limit, retryFailed ?? false),
            cancellationToken);

        return Results.Ok(result);
    });
```

Returning `200 OK` with failure counts is acceptable in V1 because the manual command completed and produced a structured result.

## Testing

Unit tests:

- Sends only `InternalOnly` by default.
- Includes `DeliveryFailed` only when `retryFailed = true`.
- Marks attempted alerts `Delivered` after successful send.
- Marks attempted alerts `DeliveryFailed` after SMTP failure.
- Does not send when no pending alerts exist.
- Does not send when email configuration is incomplete.
- Builds HTML containing symbol, setup, score, confidence, price, title, and message.
- Prevents overlapping delivery runs if semaphore protection is implemented.

Infrastructure tests if practical:

- MailKit sender constructs message with configured from/to/subject/body.

## Risks and Open Questions

- Should `DeliveryFailed` retry be controlled by query parameter or request body?
- Should `DeliveredAtUtc` be persisted in a later schema?
- Should delivery send one email per batch or one email per alert?
- Should multiple recipients be supported in V1?
- Should alert delivery be included in the manual system cycle later?

## Rollback Plan

- Remove API mapping.
- Remove delivery service and models.
- Remove MailKit sender and package.
- Remove repository delivery methods if unused.
- No database rollback expected.
