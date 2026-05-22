# Email Alert Delivery Tasks

## V1 Scope Decisions

- Backend only.
- Manual endpoint only.
- No scheduler.
- No frontend changes.
- Use MailKit SMTP.
- Send only `InternalOnly` alerts by default.
- Retry `DeliveryFailed` only when explicitly requested.
- No database schema change expected.
- Keep implementation additive.

## Backend Tasks

### 1. Add delivery statuses

- Extend `AlertDeliveryStatuses` with:
  - `Delivered`
  - `DeliveryFailed`
- Keep `InternalOnly`.

### 2. Add email delivery models

- Add `EmailAlertDeliveryRequest`.
- Add `EmailAlertDeliveryResult`.
- Add `EmailAlertDeliveryFailure`.
- Add `EmailMessage`.

### 3. Add email configuration options

- Add `EmailDeliveryOptions`.
- Include:
  - `SmtpHost`
  - `SmtpPort`
  - `SmtpUser`
  - `SmtpPassword`
  - `FromEmail`
  - `FromName`
  - `ToEmail`
  - `EnableSsl`
- Bind from `EmailDelivery` configuration section.
- Do not hardcode secrets.

### 4. Add repository delivery methods

Extend `IAlertEventRepository`:

- `GetPendingDeliveryAsync(limit, includeFailed)`
- `UpdateDeliveryStatusAsync(alertEventIds, deliveryStatus)`

EF implementation:

- Query `AlertEvents`.
- Include `InternalOnly`.
- Include `DeliveryFailed` only when requested.
- Exclude `Delivered`.
- Order by `CreatedAtUtc` ascending.
- Limit results.
- Bulk update statuses in a simple EF-safe way.

No-op implementation:

- Return empty pending delivery list.
- Accept status update as no-op.

### 5. Add email sender abstraction

- Add `IEmailSender`.
- Add `MailKitEmailSender` in Infrastructure.
- Add MailKit package reference.
- Use SMTP settings from `EmailDeliveryOptions`.
- Support SSL/TLS based on `EnableSsl`.

### 6. Add email content builder

- Add `AlertEmailTemplateBuilder`.
- Build compact HTML table with:
  - createdAtUtc
  - symbol
  - setup
  - score
  - confidence
  - priceAtSignal
  - title
  - message
  - reason summary
- Escape HTML values.
- Keep `ReasonJson` summary short.

### 7. Add delivery service

- Add `IEmailAlertDeliveryService`.
- Add `EmailAlertDeliveryService`.
- Inject:
  - `IAlertEventRepository`
  - `IEmailSender`
  - `IOptions<EmailDeliveryOptions>`
  - logger
- Add minimal `SemaphoreSlim` overlap protection.
- Return structured result on every normal failure.

### 8. Implement delivery flow

- Load pending alerts.
- If none, return zero-count result.
- Validate email configuration.
- Build email.
- Send email.
- On success:
  - update selected alerts to `Delivered`
  - return delivered counts
- On SMTP failure:
  - update selected alerts to `DeliveryFailed`
  - return failed counts and failure messages

### 9. Add API endpoint

Add:

```text
POST /api/alerts/deliver/email
```

Support:

- `limit`
- `retryFailed`

Return `EmailAlertDeliveryResult`.

### 10. Register DI

- Register `IEmailAlertDeliveryService`.
- Register `IEmailSender`.
- Configure `EmailDeliveryOptions`.
- Follow existing DI style in `Program.cs`.

### 11. Add configuration template

- Add non-secret placeholders to `appsettings.json` or `appsettings.Development.json`.
- Do not commit real SMTP credentials.
- Prefer documenting environment variable names.

### 12. Add tests

Unit tests:

- Delivers pending internal alerts.
- Does not include failed alerts by default.
- Includes failed alerts when `retryFailed = true`.
- Marks delivered after successful send.
- Marks failed after send exception.
- Does not send when no alerts exist.
- Handles missing config safely.
- HTML contains required fields.

## Validation Tasks

Backend:

```text
dotnet test tests/MarketAgent.UnitTests/MarketAgent.UnitTests.csproj --no-restore
dotnet build MarketAgent.sln --no-restore
```

Manual validation:

1. Configure SMTP settings through environment variables or local appsettings.
2. Run:

```text
POST /api/alerts/evaluate
POST /api/alerts/deliver/email
GET /api/alerts
```

3. Confirm:
   - email is received
   - delivered alerts show `DeliveryStatus = Delivered`
   - re-running delivery does not send duplicates
   - retry failed requires `retryFailed=true`

## Risks

- SMTP provider settings may vary.
- Missing config should fail clearly without marking alerts delivered.
- Batch-level success can only mark all attempted alerts together.
- Repository update failure after successful SMTP send can lead to duplicate send on later retry.
- Large batches may produce large emails.

## Open Questions

- Should failed delivery error details be persisted later?
- Should delivery status include `DeliveryInProgress` for stronger concurrency control?
- Should multiple recipient emails be supported in V1?
- Should email delivery become part of `POST /api/system/run-cycle` later?
- Should failed delivery retries use a separate endpoint or query flag?

## Rollback Plan

Backend:

- Remove `POST /api/alerts/deliver/email`.
- Remove `EmailAlertDeliveryService`.
- Remove `IEmailAlertDeliveryService`.
- Remove `IEmailSender` and MailKit implementation.
- Remove email delivery models/options/template builder.
- Remove repository delivery methods if unused.
- Remove MailKit package reference.

Database:

- No rollback expected.

Frontend:

- No rollback expected.
