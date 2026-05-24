# Automated Cycle Scheduler Design

## Current Architecture Context

MarketAgent already exposes:

- `POST /api/system/run-cycle`
- `IManualSystemCycleService`
- persisted `AlertEvents`
- email alert delivery through `IEmailAlertDeliveryService`

The scheduler should orchestrate existing application services directly. It should not call the API over HTTP.

## Configuration

Add options:

```csharp
public sealed class MarketAgentSchedulerOptions
{
    public const string SectionName = "MarketAgentScheduler";

    public bool Enabled { get; set; } = false;
    public int IntervalMinutes { get; set; } = 5;
    public bool RunEmailDelivery { get; set; } = false;
    public bool MarketHoursOnly { get; set; } = true;
}
```

Validation rules:

- `IntervalMinutes <= 0` should fall back to `5` or fail validation clearly.
- Scheduler should do nothing when `Enabled = false`.
- Keep defaults conservative.

## Hosted Service

Add a backend hosted service:

```csharp
public sealed class MarketAgentCycleSchedulerService : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken);
}
```

Dependencies:

- `IServiceScopeFactory`
- configured `MarketAgentSchedulerOptions`
- `ILogger<MarketAgentCycleSchedulerService>`

Use a scope per run to resolve:

- `IManualSystemCycleService`
- optional `IEmailAlertDeliveryService`

This keeps scoped dependencies safe and matches common ASP.NET Core hosted-service patterns.

## Execution Flow

On startup:

1. Read scheduler options.
2. If disabled, log once and exit or wait without running.
3. If enabled, enter an interval loop.

For each tick:

1. Check market-hours policy if enabled.
2. Check overlap guard.
3. Run `IManualSystemCycleService.RunAsync(...)`.
4. If cycle succeeds and `RunEmailDelivery = true`, run email delivery.
5. Log result and duration.
6. Wait until next interval.

Use `PeriodicTimer` if available:

```csharp
using var timer = new PeriodicTimer(TimeSpan.FromMinutes(intervalMinutes));
while (await timer.WaitForNextTickAsync(stoppingToken))
{
    await RunOnceAsync(stoppingToken);
}
```

Optional startup behavior:

- V1 can wait for the first interval before running.
- If immediate startup refresh is desired later, add `RunOnStartup`.

## Cycle Request

The scheduler should reuse safe defaults:

```csharp
new ManualSystemCycleRequest(
    OutcomeLimit: null,
    AlertLimit: null);
```

Do not add scheduler-specific outcome or alert limits in V1 unless the existing cycle service requires them.

## Email Delivery

If `RunEmailDelivery = true`, run delivery after the cycle.

Recommended behavior:

- Run email delivery only when `ManualSystemCycleResult.OverallSuccess = true`.
- Use default email delivery request values.
- Do not retry failed email alerts automatically in V1 unless explicitly configured later.

Example:

```csharp
new EmailAlertDeliveryRequest(
    Limit: null,
    RetryFailed: false,
    SinceMinutes: null);
```

The email delivery service already owns stale filtering, deduplication, and duplicate-send protection.

## Overlap Protection

Use a private in-process `SemaphoreSlim`.

Behavior:

- If a previous scheduled run is still active, skip the new tick.
- Log the skipped tick.
- Do not queue up missed cycles.

This is enough for single-process V1. Distributed locks are out of scope.

## Market Hours

`MarketHoursOnly` is optional but preferred.

V1 policy can be intentionally simple:

- Use UTC time.
- Approximate US regular market hours as 13:30-20:00 UTC during daylight saving time.
- Or use a small helper that maps Eastern time with `TimeZoneInfo`.
- Skip Saturdays and Sundays.

Recommended helper:

```csharp
public interface IMarketHoursService
{
    bool IsMarketOpen(DateTime utcNow);
}
```

V1 can implement regular US equity hours:

- Monday-Friday
- 09:30-16:00 America/New_York

Holiday awareness can be deferred.

Open question:

- If MarketAgent watches crypto or non-US assets, market-hours gating may need to be disabled or expanded later.

## Logging

Log:

- scheduler disabled
- scheduler started with interval/config
- market-hours skip
- overlap skip
- cycle run start
- cycle run completion with `overallSuccess`, duration, executed steps
- email delivery start/completion when enabled
- exceptions with stack details

Use UTC timestamps.

## Error Handling

- Catch exceptions inside each scheduled tick so the hosted service loop keeps running.
- Respect cancellation token during shutdown.
- Do not crash the API process because one scheduled cycle fails.
- Let `IManualSystemCycleService` handle per-step failures as it already does.

## Registration

Add options binding and hosted service registration in API startup:

```csharp
builder.Services.Configure<MarketAgentSchedulerOptions>(
    builder.Configuration.GetSection(MarketAgentSchedulerOptions.SectionName));

builder.Services.AddHostedService<MarketAgentCycleSchedulerService>();
```

The hosted service can always be registered if it exits cleanly when disabled.

## API Changes

No public API changes are required for V1.

Existing endpoints remain:

- `POST /api/system/run-cycle`
- `POST /api/alerts/deliver/email`

## Testing

Unit tests should cover:

- disabled scheduler does not run cycle
- enabled scheduler invokes cycle
- market-hours skip prevents cycle
- overlap guard skips concurrent run
- email delivery runs only when configured and cycle succeeds
- scheduler catches cycle exceptions and keeps loop alive if testable

If testing the real `BackgroundService` loop is awkward, isolate run-once logic into a small method/service that can be tested directly.

## Risks and Open Questions

- Market hours are approximate unless holiday calendars are added.
- Running every 5 minutes may increase market-data API usage.
- Multiple API instances would each run their own scheduler without a distributed lock.
- Automatic email delivery depends on SMTP config and may generate noise if alert rules are too broad.
- Manual and scheduled cycles may overlap unless both share a common guard or the scheduler relies on the cycle service's own guard.

## Rollback Plan

- Set `MarketAgentScheduler:Enabled = false`.
- Remove `AddHostedService` registration.
- Remove scheduler service/options/helper classes.
- Existing manual endpoints continue to work.
- No migration rollback expected.
