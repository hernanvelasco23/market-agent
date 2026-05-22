# Email Alert Delivery

## Problem

MarketAgent persists alert events internally, but those alerts are not delivered to the user.

Current persisted alerts use:

```text
DeliveryStatus = InternalOnly
```

This creates an audit trail, but the user still has to inspect the API or database to see alert events.

## Goal

Deliver persisted alert events by email.

V1 should add a manual backend endpoint that sends undelivered alert events in a compact HTML email and updates their delivery status after successful send.

Suggested endpoint:

```text
POST /api/alerts/deliver/email
```

## V1 Scope

- Backend only.
- Manual delivery endpoint first.
- No scheduler/background worker.
- No frontend changes.
- Send only undelivered alert events by default.
- Update `DeliveryStatus` after successful send.
- Avoid duplicate sends.
- Keep implementation additive.
- Use configuration from appsettings/environment variables.
- Never hardcode secrets.

## Delivery Statuses

Current:

```text
InternalOnly
```

Add:

```text
Delivered
DeliveryFailed
```

V1 delivery rules:

- Default sends only alerts with `DeliveryStatus = InternalOnly`.
- Alerts with `DeliveryStatus = Delivered` are skipped.
- Alerts with `DeliveryStatus = DeliveryFailed` are retried only when retry is explicitly requested.

## Configuration

Use appsettings and environment variables.

Suggested configuration section:

```json
{
  "EmailDelivery": {
    "SmtpHost": "",
    "SmtpPort": 587,
    "SmtpUser": "",
    "SmtpPassword": "",
    "FromEmail": "",
    "FromName": "MarketAgent",
    "ToEmail": "",
    "EnableSsl": true
  }
}
```

Secrets should be provided through user secrets, environment variables, or deployment configuration.

## Preferred Implementation

Use MailKit SMTP for V1.

Reasons:

- Actively used SMTP client library.
- Supports authenticated SMTP and TLS.
- Avoids relying on obsolete .NET SMTP APIs.
- Keeps provider choice flexible.

## Email Content

Send one compact HTML email per delivery run containing a table of alerts.

Fields:

- symbol
- setup
- score
- confidence
- priceAtSignal
- title
- message
- reason summary
- createdAtUtc

Plain text alternative can be added later, but HTML is enough for V1 if implementation time is constrained.

## Response Shape

Suggested response:

```json
{
  "startedAtUtc": "2026-05-22T13:00:00Z",
  "finishedAtUtc": "2026-05-22T13:00:04Z",
  "attemptedCount": 5,
  "deliveredCount": 5,
  "failedCount": 0,
  "skippedCount": 0,
  "failures": []
}
```

Failure item:

```json
{
  "alertEventId": "00000000-0000-0000-0000-000000000000",
  "symbol": "NVDA",
  "message": "SMTP authentication failed."
}
```

## Schema Decision

Do not add database schema in V1 unless implementation proves it is necessary.

The existing `AlertEvents` table has `DeliveryStatus`, which is enough for:

- pending internal alerts
- delivered alerts
- failed alerts

If failure details need persistence later, add a separate proposal for:

- `DeliveryAttemptCount`
- `LastDeliveryAttemptAtUtc`
- `LastDeliveryError`
- `DeliveredAtUtc`

V1 should use logs and API response failure summaries instead.

## Out of Scope

- No scheduler.
- No frontend changes.
- No alert evaluation rule changes.
- No signal generation changes.
- No outcome evaluation changes.
- No Discord/Telegram/SMS delivery.
- No per-user routing.
- No delivery templates editor.
- No persisted delivery attempt history.

## Success Criteria

- `POST /api/alerts/deliver/email` sends a compact email for undelivered alerts.
- Successful sends update alert events to `DeliveryStatus = Delivered`.
- Failed sends update attempted alerts to `DeliveryStatus = DeliveryFailed`.
- Delivered alerts are not sent again by default.
- Retry of failed alerts requires explicit request.
- Existing alert evaluation behavior remains unchanged.

## Risks

- SMTP configuration can fail in many environment-specific ways.
- Updating all alerts to `Delivered` after a single batch email means partial SMTP acceptance cannot be tracked per recipient in V1.
- Without persisted failure details, debugging depends on logs and response payloads.
- Retrying failed alerts without explicit user intent could cause duplicate emails.
- Large alert batches could produce overly long emails.

## Rollback Plan

Backend rollback:

- Remove `POST /api/alerts/deliver/email`.
- Remove email delivery service and SMTP sender.
- Remove email delivery models/options.
- Remove repository methods for delivery status query/update if unused.
- Remove MailKit package reference.

Database rollback:

- No schema rollback expected in V1.
- Existing `DeliveryStatus` values can remain.

Frontend rollback:

- None expected because V1 has no frontend changes.
