# Automated Cycle Scheduler Tasks

## 1. Configuration

- [ ] Add `MarketAgentSchedulerOptions`.
- [ ] Bind `MarketAgentScheduler` config section.
- [ ] Add safe placeholder config to `appsettings.json`.
- [ ] Add local-development override in `appsettings.Development.json` if useful.
- [ ] Keep `Enabled = false` by default.

## 2. Scheduler Service

- [ ] Add `MarketAgentCycleSchedulerService` as a `BackgroundService`.
- [ ] Use `IServiceScopeFactory` to resolve scoped services per run.
- [ ] Use configured interval minutes with default `5`.
- [ ] Respect cancellation token during shutdown.
- [ ] Log scheduler startup and disabled state.

## 3. Cycle Execution

- [ ] Call `IManualSystemCycleService.RunAsync` directly.
- [ ] Use default `ManualSystemCycleRequest` values.
- [ ] Do not call internal HTTP endpoints.
- [ ] Log cycle result, duration, executed step count, and failure step.
- [ ] Catch unexpected exceptions so the scheduler loop survives.

## 4. Overlap Protection

- [ ] Add minimal in-process `SemaphoreSlim` guard.
- [ ] Skip a scheduled tick if a prior run is still active.
- [ ] Log overlap skips.
- [ ] Do not queue missed runs.

## 5. Market Hours

- [ ] Add simple market-hours helper or service.
- [ ] When `MarketHoursOnly = true`, skip runs outside regular market hours.
- [ ] Use US equity regular hours for V1.
- [ ] Skip weekends.
- [ ] Document that holidays are not handled in V1.

## 6. Optional Email Delivery

- [ ] If `RunEmailDelivery = true`, resolve `IEmailAlertDeliveryService`.
- [ ] Run email delivery only after a successful cycle.
- [ ] Use default `EmailAlertDeliveryRequest`.
- [ ] Do not retry failed email alerts automatically in V1.
- [ ] Log email delivery result.

## 7. DI Registration

- [ ] Register scheduler options.
- [ ] Register market-hours helper if added.
- [ ] Register hosted scheduler service.
- [ ] Ensure scheduler is inert when disabled.

## 8. Tests

- [ ] Disabled scheduler does not execute cycle.
- [ ] Enabled scheduler run-once path executes cycle.
- [ ] Market-hours closed state skips cycle.
- [ ] Overlap guard skips concurrent run.
- [ ] Email delivery runs only when configured and cycle succeeds.
- [ ] Email delivery does not run when cycle fails.
- [ ] Exceptions are logged and contained.

## 9. Validation

- [ ] Run `dotnet test tests/MarketAgent.UnitTests/MarketAgent.UnitTests.csproj --no-restore`.
- [ ] Run `dotnet build MarketAgent.sln --no-restore`.
- [ ] Manually set `MarketAgentScheduler:Enabled = true` in development config.
- [ ] Start API and confirm scheduler logs show interval execution.
- [ ] Confirm manual `POST /api/system/run-cycle` still works.
- [ ] If email delivery is enabled, confirm emails are delivered only after successful cycles.

## 10. Rollback

- [ ] Set `MarketAgentScheduler:Enabled = false`.
- [ ] Remove hosted service registration if a code rollback is needed.
- [ ] Remove scheduler options/service/helper classes.
- [ ] No database rollback expected.
