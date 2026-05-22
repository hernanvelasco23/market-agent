using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Models;
using Microsoft.Extensions.Logging;

namespace MarketAgent.Application.Alerts;

public sealed class EmailAlertDeliveryService : IEmailAlertDeliveryService
{
    public const int DefaultLimit = 8;
    public const int MaxLimit = 100;
    public const int DefaultSinceMinutes = 180;
    private const string DeliveryStepName = "EmailDelivery";
    private static readonly SemaphoreSlim DeliveryLock = new(1, 1);

    private readonly IAlertEventRepository _alertEventRepository;
    private readonly IEmailSender _emailSender;
    private readonly EmailDeliveryOptions _options;
    private readonly ILogger<EmailAlertDeliveryService> _logger;

    public EmailAlertDeliveryService(
        IAlertEventRepository alertEventRepository,
        IEmailSender emailSender,
        EmailDeliveryOptions options,
        ILogger<EmailAlertDeliveryService> logger)
    {
        _alertEventRepository = alertEventRepository;
        _emailSender = emailSender;
        _options = options;
        _logger = logger;
    }

    public async Task<EmailAlertDeliveryResult> DeliverAsync(
        EmailAlertDeliveryRequest request,
        CancellationToken cancellationToken = default)
    {
        var startedAtUtc = DateTime.UtcNow;

        if (!await DeliveryLock.WaitAsync(0, cancellationToken))
        {
            return BuildResult(
                startedAtUtc,
                attemptedCount: 0,
                deliveredCount: 0,
                failedCount: 0,
                skippedCount: 0,
                staleSkippedCount: 0,
                duplicateSkippedCount: 0,
                [new EmailAlertDeliveryFailure(Guid.Empty, DeliveryStepName, "Email alert delivery is already running.")]);
        }

        try
        {
            var limit = NormalizeLimit(request.Limit);
            var sinceUtc = ResolveSinceUtc(request.SinceMinutes);
            var staleSkippedCount = sinceUtc is null
                ? 0
                : await _alertEventRepository.CountStalePendingDeliveryAsync(
                    request.RetryFailed,
                    sinceUtc.Value,
                    cancellationToken);
            var alerts = await _alertEventRepository.GetPendingDeliveryAsync(
                MaxLimit,
                request.RetryFailed,
                sinceUtc,
                cancellationToken);

            if (alerts.Count == 0)
            {
                return BuildResult(startedAtUtc, 0, 0, 0, 0, staleSkippedCount, 0, []);
            }

            var selection = SelectDigestAlerts(alerts, limit);
            var selectedAlerts = selection.Alerts;
            var duplicateSkippedCount = selection.DuplicateSkippedCount;

            var configError = ValidateOptions(_options);
            if (configError is not null)
            {
                _logger.LogWarning("Email alert delivery skipped because configuration is incomplete: {ConfigError}.", configError);

                return BuildResult(
                    startedAtUtc,
                    attemptedCount: 0,
                    deliveredCount: 0,
                    failedCount: 0,
                    skippedCount: selectedAlerts.Count,
                    staleSkippedCount,
                    duplicateSkippedCount,
                    [new EmailAlertDeliveryFailure(Guid.Empty, DeliveryStepName, configError)]);
            }

            var message = AlertEmailTemplateBuilder.BuildMessage(selectedAlerts, _options, DateTime.UtcNow);
            var alertIds = selectedAlerts.Select(alert => alert.Id).ToArray();

            try
            {
                await _emailSender.SendAsync(message, cancellationToken);
                await _alertEventRepository.UpdateDeliveryStatusAsync(
                    alertIds,
                    AlertDeliveryStatuses.Delivered,
                    cancellationToken);

                _logger.LogInformation("Delivered {AlertCount} alert events by email.", selectedAlerts.Count);

                return BuildResult(startedAtUtc, selectedAlerts.Count, selectedAlerts.Count, 0, 0, staleSkippedCount, duplicateSkippedCount, []);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(exception, "Email alert delivery failed for {AlertCount} alert events.", selectedAlerts.Count);

                await _alertEventRepository.UpdateDeliveryStatusAsync(
                    alertIds,
                    AlertDeliveryStatuses.DeliveryFailed,
                    cancellationToken);

                var failures = selectedAlerts
                    .Select(alert => new EmailAlertDeliveryFailure(alert.Id, alert.Symbol, exception.Message))
                    .ToArray();

                return BuildResult(startedAtUtc, selectedAlerts.Count, 0, selectedAlerts.Count, 0, staleSkippedCount, duplicateSkippedCount, failures);
            }
        }
        finally
        {
            DeliveryLock.Release();
        }
    }

    private static EmailAlertDeliveryResult BuildResult(
        DateTime startedAtUtc,
        int attemptedCount,
        int deliveredCount,
        int failedCount,
        int skippedCount,
        int staleSkippedCount,
        int duplicateSkippedCount,
        IReadOnlyCollection<EmailAlertDeliveryFailure> failures)
    {
        return new EmailAlertDeliveryResult(
            startedAtUtc,
            DateTime.UtcNow,
            attemptedCount,
            deliveredCount,
            failedCount,
            skippedCount,
            staleSkippedCount,
            duplicateSkippedCount,
            failures);
    }

    private static DigestAlertSelection SelectDigestAlerts(
        IReadOnlyCollection<AlertEventItem> alerts,
        int limit)
    {
        var groups = alerts
            .GroupBy(alert => alert.Symbol.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var duplicateSkippedCount = groups.Sum(group => Math.Max(0, group.Count() - 1));
        var selectedAlerts = groups
            .Select(SelectBestAlert)
            .OrderByDescending(alert => AlertUpsideCalculator.Calculate(alert)?.PotentialUpsidePercent ?? decimal.MinValue)
            .ThenByDescending(alert => alert.Score)
            .ThenByDescending(alert => ConfidenceRank(alert.Confidence))
            .ThenByDescending(alert => EnsureUtc(alert.CreatedAtUtc))
            .Take(limit)
            .ToArray();

        return new DigestAlertSelection(selectedAlerts, duplicateSkippedCount);
    }

    private static AlertEventItem SelectBestAlert(IEnumerable<AlertEventItem> alerts)
    {
        return alerts
            .OrderByDescending(alert => AlertUpsideCalculator.Calculate(alert)?.PotentialUpsidePercent ?? decimal.MinValue)
            .ThenByDescending(alert => alert.Score)
            .ThenByDescending(alert => ConfidenceRank(alert.Confidence))
            .ThenByDescending(alert => EnsureUtc(alert.CreatedAtUtc))
            .First();
    }

    private static int NormalizeLimit(int? limit)
    {
        return Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
    }

    private static DateTime? ResolveSinceUtc(int? sinceMinutes)
    {
        var minutes = sinceMinutes ?? DefaultSinceMinutes;
        return minutes <= 0
            ? null
            : DateTime.UtcNow.AddMinutes(-minutes);
    }

    private static string? ValidateOptions(EmailDeliveryOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.SmtpHost))
        {
            return "EmailDelivery:SmtpHost is required.";
        }

        if (options.SmtpPort <= 0)
        {
            return "EmailDelivery:SmtpPort must be greater than zero.";
        }

        if (string.IsNullOrWhiteSpace(options.FromEmail))
        {
            return "EmailDelivery:FromEmail is required.";
        }

        if (string.IsNullOrWhiteSpace(options.ToEmail))
        {
            return "EmailDelivery:ToEmail is required.";
        }

        return null;
    }

    private static int ConfidenceRank(string confidence)
    {
        return confidence.Trim() switch
        {
            var value when value.Equals("High", StringComparison.OrdinalIgnoreCase) => 3,
            var value when value.Equals("Medium", StringComparison.OrdinalIgnoreCase) => 2,
            var value when value.Equals("Low", StringComparison.OrdinalIgnoreCase) => 1,
            _ => 0
        };
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private sealed record DigestAlertSelection(
        IReadOnlyCollection<AlertEventItem> Alerts,
        int DuplicateSkippedCount);
}
