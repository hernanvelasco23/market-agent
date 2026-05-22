using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Alerts;
using MarketAgent.Application.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace MarketAgent.UnitTests;

public sealed class EmailAlertDeliveryServiceTests
{
    [Fact]
    public async Task DeliverAsync_SendsOnlyInternalOnlyAlerts_ByDefault()
    {
        var repository = new RecordingAlertEventRepository(
        [
            CreateAlert("NVDA", AlertDeliveryStatuses.InternalOnly),
            CreateAlert("TSLA", AlertDeliveryStatuses.DeliveryFailed),
            CreateAlert("MSFT", AlertDeliveryStatuses.Delivered)
        ]);
        var sender = new RecordingEmailSender();
        var service = CreateService(repository, sender);

        var result = await service.DeliverAsync(new EmailAlertDeliveryRequest(Limit: 10, RetryFailed: false, SinceMinutes: null));

        Assert.Equal(1, result.AttemptedCount);
        Assert.Equal(1, result.DeliveredCount);
        Assert.Single(sender.Messages);
        Assert.Equal([AlertDeliveryStatuses.Delivered], repository.StatusUpdates.Select(update => update.DeliveryStatus));
        Assert.Contains("NVDA", sender.Messages[0].HtmlBody);
        Assert.DoesNotContain("TSLA", sender.Messages[0].HtmlBody);
    }

    [Fact]
    public async Task DeliverAsync_IncludesFailedAlerts_WhenRetryFailedIsTrue()
    {
        var repository = new RecordingAlertEventRepository(
        [
            CreateAlert("NVDA", AlertDeliveryStatuses.InternalOnly),
            CreateAlert("TSLA", AlertDeliveryStatuses.DeliveryFailed),
            CreateAlert("MSFT", AlertDeliveryStatuses.Delivered)
        ]);
        var sender = new RecordingEmailSender();
        var service = CreateService(repository, sender);

        var result = await service.DeliverAsync(new EmailAlertDeliveryRequest(Limit: 10, RetryFailed: true, SinceMinutes: null));

        Assert.Equal(2, result.AttemptedCount);
        Assert.Equal(2, result.DeliveredCount);
        Assert.Contains("NVDA", sender.Messages[0].HtmlBody);
        Assert.Contains("TSLA", sender.Messages[0].HtmlBody);
        Assert.DoesNotContain("MSFT", sender.Messages[0].HtmlBody);
    }

    [Fact]
    public async Task DeliverAsync_MarksAlertsDeliveryFailed_WhenSenderFails()
    {
        var repository = new RecordingAlertEventRepository(
        [
            CreateAlert("NVDA", AlertDeliveryStatuses.InternalOnly),
            CreateAlert("TSLA", AlertDeliveryStatuses.InternalOnly)
        ]);
        var sender = new RecordingEmailSender(new InvalidOperationException("smtp failed"));
        var service = CreateService(repository, sender);

        var result = await service.DeliverAsync(new EmailAlertDeliveryRequest(Limit: 10, RetryFailed: false, SinceMinutes: null));

        Assert.Equal(2, result.AttemptedCount);
        Assert.Equal(0, result.DeliveredCount);
        Assert.Equal(2, result.FailedCount);
        Assert.Equal(2, result.Failures.Count);
        Assert.Equal(AlertDeliveryStatuses.DeliveryFailed, Assert.Single(repository.StatusUpdates).DeliveryStatus);
    }

    [Fact]
    public async Task DeliverAsync_MissingConfigFailsSafelyWithoutSendingOrUpdatingStatus()
    {
        var repository = new RecordingAlertEventRepository(
        [
            CreateAlert("NVDA", AlertDeliveryStatuses.InternalOnly)
        ]);
        var sender = new RecordingEmailSender();
        var service = CreateService(repository, sender, new EmailDeliveryOptions());

        var result = await service.DeliverAsync(new EmailAlertDeliveryRequest(Limit: 10, RetryFailed: false, SinceMinutes: null));

        Assert.Equal(0, result.AttemptedCount);
        Assert.Equal(0, result.DeliveredCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Single(result.Failures);
        Assert.Empty(sender.Messages);
        Assert.Empty(repository.StatusUpdates);
    }

    [Fact]
    public async Task DeliverAsync_NoPendingAlertsReturnsZeroCountResult()
    {
        var repository = new RecordingAlertEventRepository(
        [
            CreateAlert("NVDA", AlertDeliveryStatuses.Delivered)
        ]);
        var sender = new RecordingEmailSender();
        var service = CreateService(repository, sender);

        var result = await service.DeliverAsync(new EmailAlertDeliveryRequest(Limit: 10, RetryFailed: false, SinceMinutes: null));

        Assert.Equal(0, result.AttemptedCount);
        Assert.Equal(0, result.DeliveredCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Empty(sender.Messages);
    }

    [Fact]
    public async Task DeliverAsync_ExcludesStaleAlertsByDefault()
    {
        var repository = new RecordingAlertEventRepository(
        [
            CreateAlert("NVDA", AlertDeliveryStatuses.InternalOnly, createdAtUtc: DateTime.UtcNow.AddMinutes(-20)),
            CreateAlert("OLD", AlertDeliveryStatuses.InternalOnly, createdAtUtc: DateTime.UtcNow.AddHours(-8))
        ]);
        var sender = new RecordingEmailSender();
        var service = CreateService(repository, sender);

        var result = await service.DeliverAsync(new EmailAlertDeliveryRequest(Limit: 10, RetryFailed: false, SinceMinutes: null));

        Assert.Equal(1, result.AttemptedCount);
        Assert.Equal(1, result.StaleSkippedCount);
        Assert.Contains("NVDA", sender.Messages[0].HtmlBody);
        Assert.DoesNotContain("OLD", sender.Messages[0].HtmlBody);
    }

    [Fact]
    public async Task DeliverAsync_SinceMinutesZeroIncludesOldAlerts()
    {
        var repository = new RecordingAlertEventRepository(
        [
            CreateAlert("OLD", AlertDeliveryStatuses.InternalOnly, createdAtUtc: DateTime.UtcNow.AddHours(-8))
        ]);
        var sender = new RecordingEmailSender();
        var service = CreateService(repository, sender);

        var result = await service.DeliverAsync(new EmailAlertDeliveryRequest(Limit: 10, RetryFailed: false, SinceMinutes: 0));

        Assert.Equal(1, result.AttemptedCount);
        Assert.Equal(0, result.StaleSkippedCount);
        Assert.Contains("OLD", sender.Messages[0].HtmlBody);
    }

    [Fact]
    public async Task DeliverAsync_DefaultLimitIsEight()
    {
        var repository = new RecordingAlertEventRepository(
            Enumerable.Range(1, 10)
                .Select(index => CreateAlert($"S{index}", AlertDeliveryStatuses.InternalOnly, score: 80m + index))
                .ToArray());
        var sender = new RecordingEmailSender();
        var service = CreateService(repository, sender);

        var result = await service.DeliverAsync(new EmailAlertDeliveryRequest(Limit: null, RetryFailed: false, SinceMinutes: 0));

        Assert.Equal(8, result.AttemptedCount);
    }

    [Fact]
    public async Task DeliverAsync_CustomLimitOverridesDefault()
    {
        var repository = new RecordingAlertEventRepository(
            Enumerable.Range(1, 10)
                .Select(index => CreateAlert($"S{index}", AlertDeliveryStatuses.InternalOnly, score: 80m + index))
                .ToArray());
        var sender = new RecordingEmailSender();
        var service = CreateService(repository, sender);

        var result = await service.DeliverAsync(new EmailAlertDeliveryRequest(Limit: 3, RetryFailed: false, SinceMinutes: 0));

        Assert.Equal(3, result.AttemptedCount);
    }

    [Fact]
    public async Task DeliverAsync_DeduplicatesAlertsBySymbol()
    {
        var selectedNvda = CreateAlert("NVDA", AlertDeliveryStatuses.InternalOnly, score: 90m);
        var duplicateNvda = CreateAlert("nvda", AlertDeliveryStatuses.InternalOnly, score: 89m);
        var tsla = CreateAlert("TSLA", AlertDeliveryStatuses.InternalOnly, score: 88m);
        var repository = new RecordingAlertEventRepository(
        [
            selectedNvda,
            duplicateNvda,
            tsla
        ]);
        var sender = new RecordingEmailSender();
        var service = CreateService(repository, sender);

        var result = await service.DeliverAsync(new EmailAlertDeliveryRequest(Limit: 8, RetryFailed: false, SinceMinutes: 0));

        Assert.Equal(2, result.AttemptedCount);
        Assert.Equal(1, result.DuplicateSkippedCount);
        Assert.Single(repository.StatusUpdates);
        Assert.Equal(2, repository.StatusUpdates[0].AlertEventIds.Count);
        Assert.Contains(selectedNvda.Id, repository.StatusUpdates[0].AlertEventIds);
        Assert.Contains(tsla.Id, repository.StatusUpdates[0].AlertEventIds);
        Assert.DoesNotContain(duplicateNvda.Id, repository.StatusUpdates[0].AlertEventIds);
    }

    [Fact]
    public async Task DeliverAsync_SelectsBestDuplicateByHighestUpsideFirst()
    {
        var lowUpside = CreateAlert("NVDA", AlertDeliveryStatuses.InternalOnly, score: 99m, entry: 100m, takeProfit2: 110m);
        var highUpside = CreateAlert("NVDA", AlertDeliveryStatuses.InternalOnly, score: 80m, entry: 100m, takeProfit2: 130m);
        var repository = new RecordingAlertEventRepository([lowUpside, highUpside]);
        var sender = new RecordingEmailSender();
        var service = CreateService(repository, sender);

        await service.DeliverAsync(new EmailAlertDeliveryRequest(Limit: 8, RetryFailed: false, SinceMinutes: 0));

        Assert.Equal([highUpside.Id], repository.StatusUpdates[0].AlertEventIds);
        Assert.Contains("+30.00%", sender.Messages[0].HtmlBody);
        Assert.DoesNotContain("+10.00%", sender.Messages[0].HtmlBody);
    }

    [Fact]
    public async Task DeliverAsync_SelectsBestDuplicateByScore_WhenUpsideIsMissing()
    {
        var lowScore = CreateAlert("NVDA", AlertDeliveryStatuses.InternalOnly, score: 80m, entry: null, takeProfit2: null);
        var highScore = CreateAlert("NVDA", AlertDeliveryStatuses.InternalOnly, score: 95m, entry: null, takeProfit2: null);
        var repository = new RecordingAlertEventRepository([lowScore, highScore]);
        var sender = new RecordingEmailSender();
        var service = CreateService(repository, sender);

        await service.DeliverAsync(new EmailAlertDeliveryRequest(Limit: 8, RetryFailed: false, SinceMinutes: 0));

        Assert.Equal([highScore.Id], repository.StatusUpdates[0].AlertEventIds);
        Assert.Contains("95", sender.Messages[0].HtmlBody);
    }

    [Fact]
    public async Task DeliverAsync_AppliesLimitAfterDeduplication()
    {
        var alerts = Enumerable.Range(1, 10)
            .SelectMany(index => new[]
            {
                CreateAlert($"S{index}", AlertDeliveryStatuses.InternalOnly, score: 80m + index, entry: 100m, takeProfit2: 120m + index),
                CreateAlert($"S{index}", AlertDeliveryStatuses.InternalOnly, score: 70m + index, entry: 100m, takeProfit2: 110m + index)
            })
            .ToArray();
        var repository = new RecordingAlertEventRepository(alerts);
        var sender = new RecordingEmailSender();
        var service = CreateService(repository, sender);

        var result = await service.DeliverAsync(new EmailAlertDeliveryRequest(Limit: 8, RetryFailed: false, SinceMinutes: 0));

        Assert.Equal(8, result.AttemptedCount);
        Assert.Equal(10, result.DuplicateSkippedCount);
        Assert.Equal(8, repository.StatusUpdates[0].AlertEventIds.Count);
    }

    [Fact]
    public void BuildMessage_CreatesDynamicSubjectAndCompactHtml()
    {
        var alerts = new[]
        {
            CreateAlert("NVDA", AlertDeliveryStatuses.InternalOnly, score: 93m),
            CreateAlert("TSLA", AlertDeliveryStatuses.InternalOnly, score: 88m)
        };
        var message = AlertEmailTemplateBuilder.BuildMessage(
            alerts,
            ValidOptions(),
            DateTime.SpecifyKind(new DateTime(2026, 5, 22, 12, 0, 0), DateTimeKind.Utc));

        Assert.Equal("[MarketAgent] 2 New Alerts - NVDA, TSLA", message.Subject);
        Assert.Contains("MarketAgent Alert Digest", message.HtmlBody);
        Assert.Contains("NVDA", message.HtmlBody);
        Assert.Contains("MomentumContinuation", message.HtmlBody);
        Assert.Contains("93", message.HtmlBody);
        Assert.Contains("HIGH CONFIDENCE", message.HtmlBody);
        Assert.Contains("123.45", message.HtmlBody);
        Assert.Contains("Alert title", message.HtmlBody);
        Assert.Contains("Alert message", message.HtmlBody);
        Assert.Contains("setup avg 15m", message.HtmlBody);
        Assert.Contains("+21.05%", message.HtmlBody);
        Assert.Contains("HIGH UPSIDE", message.HtmlBody);
        Assert.Contains("Best Upside", message.HtmlBody);
        Assert.DoesNotContain("ruleDecisions", message.HtmlBody);
    }

    [Fact]
    public void BuildMessage_UsesSingularSubjectForOneAlert()
    {
        var message = AlertEmailTemplateBuilder.BuildMessage(
            [CreateAlert("NVDA", AlertDeliveryStatuses.InternalOnly)],
            ValidOptions(),
            DateTime.UtcNow);

        Assert.Equal("[MarketAgent] 1 New Alert - NVDA", message.Subject);
    }

    private static EmailAlertDeliveryService CreateService(
        RecordingAlertEventRepository repository,
        RecordingEmailSender sender,
        EmailDeliveryOptions? options = null)
    {
        return new EmailAlertDeliveryService(
            repository,
            sender,
            options ?? ValidOptions(),
            NullLogger<EmailAlertDeliveryService>.Instance);
    }

    private static EmailDeliveryOptions ValidOptions()
    {
        return new EmailDeliveryOptions
        {
            SmtpHost = "smtp.example.test",
            SmtpPort = 587,
            FromEmail = "alerts@example.test",
            FromName = "MarketAgent",
            ToEmail = "user@example.test",
            EnableSsl = true
        };
    }

    private static AlertEventItem CreateAlert(
        string symbol,
        string deliveryStatus,
        decimal score = 91m,
        DateTime? createdAtUtc = null,
        decimal? entry = 95m,
        decimal? takeProfit1 = 105m,
        decimal? takeProfit2 = 115m,
        decimal? takeProfit3 = 120m)
    {
        return new AlertEventItem(
            Guid.NewGuid(),
            DateTime.SpecifyKind(createdAtUtc ?? DateTime.UtcNow, DateTimeKind.Utc),
            Guid.NewGuid(),
            Guid.NewGuid(),
            symbol,
            "MomentumContinuation",
            "Candidate",
            score,
            score >= 90m ? "High" : "Medium",
            123.45m,
            AlertTypes.HighQualityCandidate,
            "Alert title",
            "Alert message",
            """
            {
              "setupAverageReturn15m": 1.25,
              "minimumScore": 85,
              "confidence": "High",
              "ruleDecisions": {
                "meetsSetupPerformance": true
              }
            }
            """,
            deliveryStatus,
            Entry: entry,
            TakeProfit1: takeProfit1,
            TakeProfit2: takeProfit2,
            TakeProfit3: takeProfit3,
            RiskReward1: 1.5m,
            RiskReward2: 2.5m,
            RiskReward3: 3.5m);
    }

    private sealed class RecordingEmailSender : IEmailSender
    {
        private readonly Exception? _exception;

        public RecordingEmailSender(Exception? exception = null)
        {
            _exception = exception;
        }

        public List<EmailMessage> Messages { get; } = [];

        public Task SendAsync(
            EmailMessage message,
            CancellationToken cancellationToken = default)
        {
            if (_exception is not null)
            {
                throw _exception;
            }

            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingAlertEventRepository : IAlertEventRepository
    {
        private readonly List<AlertEventItem> _alerts;

        public RecordingAlertEventRepository(IReadOnlyCollection<AlertEventItem> alerts)
        {
            _alerts = alerts.ToList();
        }

        public List<(IReadOnlyCollection<Guid> AlertEventIds, string DeliveryStatus)> StatusUpdates { get; } = [];

        public Task<IReadOnlyCollection<AlertEventItem>> GetRecentAsync(
            int limit,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyCollection<AlertEventItem>> GetPendingDeliveryAsync(
            int limit,
            bool includeFailed,
            DateTime? sinceUtc,
            CancellationToken cancellationToken = default)
        {
            var statuses = includeFailed
                ? new[] { AlertDeliveryStatuses.InternalOnly, AlertDeliveryStatuses.DeliveryFailed }
                : [AlertDeliveryStatuses.InternalOnly];

            return Task.FromResult<IReadOnlyCollection<AlertEventItem>>(
                _alerts
                    .Where(alert => statuses.Contains(alert.DeliveryStatus))
                    .Where(alert => sinceUtc is null || alert.CreatedAtUtc >= sinceUtc.Value)
                    .OrderByDescending(alert => alert.Score)
                    .ThenByDescending(alert => ConfidenceRank(alert.Confidence))
                    .ThenByDescending(alert => alert.CreatedAtUtc)
                    .ThenBy(alert => alert.Symbol, StringComparer.OrdinalIgnoreCase)
                    .Take(limit)
                    .ToArray());
        }

        public Task<int> CountStalePendingDeliveryAsync(
            bool includeFailed,
            DateTime sinceUtc,
            CancellationToken cancellationToken = default)
        {
            var statuses = includeFailed
                ? new[] { AlertDeliveryStatuses.InternalOnly, AlertDeliveryStatuses.DeliveryFailed }
                : [AlertDeliveryStatuses.InternalOnly];

            return Task.FromResult(_alerts.Count(alert =>
                statuses.Contains(alert.DeliveryStatus) &&
                alert.CreatedAtUtc < sinceUtc));
        }

        public Task<IReadOnlySet<string>> GetExistingAlertKeysAsync(
            IReadOnlyCollection<AlertEvaluationCandidate> candidates,
            string alertType,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task SaveAsync(
            AlertEventRecord alertEvent,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task UpdateDeliveryStatusAsync(
            IReadOnlyCollection<Guid> alertEventIds,
            string deliveryStatus,
            CancellationToken cancellationToken = default)
        {
            StatusUpdates.Add((alertEventIds, deliveryStatus));
            return Task.CompletedTask;
        }

        private static int ConfidenceRank(string confidence)
        {
            return confidence switch
            {
                "High" => 3,
                "Medium" => 2,
                "Low" => 1,
                _ => 0
            };
        }
    }
}
