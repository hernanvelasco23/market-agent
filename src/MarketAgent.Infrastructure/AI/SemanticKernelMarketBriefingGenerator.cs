using System.Text;
using System.Text.Json;
using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Models;
using MarketAgent.Domain.Entities;
using MarketAgent.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;

namespace MarketAgent.Infrastructure.AI;

public sealed class SemanticKernelMarketBriefingGenerator : IMarketBriefingGenerator
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AzureOpenAIOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILoggerFactory _loggerFactory;

    public SemanticKernelMarketBriefingGenerator(
        IOptions<AzureOpenAIOptions> options,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _loggerFactory = loggerFactory;
    }

    public async Task<MarketBriefingResult> GenerateAsync(
        IReadOnlyCollection<MarketSnapshot> snapshots,
        IReadOnlyCollection<MarketSignal> signals,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        ArgumentNullException.ThrowIfNull(signals);

        if (snapshots.Count == 0)
        {
            throw new InvalidOperationException("At least one market snapshot is required to generate a briefing.");
        }

        var chatCompletionService = CreateChatCompletionService();
        var prompt = BuildPrompt(snapshots, signals);

        var response = await chatCompletionService.GetChatMessageContentAsync(
            prompt,
            cancellationToken: cancellationToken);

        var content = response.Content?.Trim();

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("The AI market briefing response was empty.");
        }

        var briefing = ParseBriefing(content);
        var highlights = briefing.Highlights ?? [];
        var risks = briefing.Risks ?? [];
        var watchItems = briefing.WatchItems ?? [];
        var topOpportunities = BuildTopOpportunities(signals);
        var watchlistPullbacks = BuildWatchlistPullbacks(signals);
        var topRisks = BuildTopRisks(signals);

        return new MarketBriefingResult(
            DateTime.UtcNow,
            briefing.MarketRegime,
            briefing.Summary,
            briefing.SignalSummary,
            topOpportunities,
            watchlistPullbacks,
            topRisks,
            highlights,
            risks,
            watchItems);
    }

    private IChatCompletionService CreateChatCompletionService()
    {
        if (string.IsNullOrWhiteSpace(_options.Endpoint) ||
            string.IsNullOrWhiteSpace(_options.DeploymentName) ||
            string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException(
                "Azure OpenAI configuration is incomplete. Set AzureOpenAI:Endpoint, AzureOpenAI:DeploymentName, and AzureOpenAI:ApiKey.");
        }

        return new AzureOpenAIChatCompletionService(
            _options.DeploymentName,
            _options.Endpoint,
            _options.ApiKey,
            _options.ModelId,
            _httpClientFactory.CreateClient(nameof(SemanticKernelMarketBriefingGenerator)),
            _loggerFactory,
            _options.ApiVersion);
    }

    private static string BuildPrompt(
        IReadOnlyCollection<MarketSnapshot> snapshots,
        IReadOnlyCollection<MarketSignal> signals)
    {
        var promptBuilder = new StringBuilder();

        promptBuilder.AppendLine("You are Market Agent.");
        promptBuilder.AppendLine("Use the calculated MarketSignal results as the primary input, with MarketSnapshot data as supporting context.");
        promptBuilder.AppendLine("Explain only calculated data. Do not invent prices, indicators, RSI values, targets, stops, scores, or recommendations.");
        promptBuilder.AppendLine("Do not give trading advice, buy/sell recommendations, or unsupported claims.");
        promptBuilder.AppendLine("This is a decision-support signal, not a trade recommendation.");
        promptBuilder.AppendLine("Use the preclassified signal sections below to prioritize top opportunities, watchlist pullbacks, top risks, strongest assets, weakest assets, and watch items.");
        promptBuilder.AppendLine("Do not move a risk asset into opportunity language. Low-score pullbacks require confirmation and must not sound attractive.");
        promptBuilder.AppendLine("Return valid JSON only with this shape:");
        promptBuilder.AppendLine("""{"marketRegime":"string","summary":"string","signalSummary":"string","highlights":["string"],"risks":["string"],"watchItems":["string"]}""");
        promptBuilder.AppendLine("The API response will attach topOpportunities, watchlistPullbacks, and topRisks from calculated signals. Do not create those arrays in the AI JSON.");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Snapshots:");

        foreach (var snapshot in snapshots.OrderByDescending(item => item.CapturedAtUtc))
        {
            promptBuilder.AppendLine(
                $"- Symbol: {snapshot.Symbol}; Type: {snapshot.AssetType}; Price: {snapshot.Price.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)}; Currency: {snapshot.Currency}; CapturedAtUtc: {snapshot.CapturedAtUtc:O}; Source: {snapshot.Source}; Open: {FormatOptional(snapshot.OpenPrice)}; High: {FormatOptional(snapshot.HighPrice)}; Low: {FormatOptional(snapshot.LowPrice)}; PreviousClose: {FormatOptional(snapshot.PreviousClose)}; Volume: {FormatOptional(snapshot.Volume)}");
        }

        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Calculated signal sections:");
        AppendSignalSection(
            promptBuilder,
            "Top opportunities: bullish signals. Score >= 60 near the session high has Medium confidence, Intraday timeframe, and Candidate action.",
            BuildTopOpportunities(signals));
        AppendSignalSection(
            promptBuilder,
            "Watchlist pullbacks: pullback setups with score between 40 and 55. Confidence is Low, timeframe is WatchOnly, and action is Watch for confirmation.",
            BuildWatchlistPullbacks(signals));
        AppendRiskSection(
            promptBuilder,
            "Top risks: weak or risky assets with score < 40. Confidence is Low, timeframe is WatchOnly, and action is Avoid / high risk.",
            BuildTopRisks(signals));

        promptBuilder.AppendLine();
        promptBuilder.AppendLine("All calculated signals:");

        foreach (var signal in signals.OrderByDescending(item => item.Score))
        {
            promptBuilder.AppendLine(
                $"- Symbol: {signal.Symbol}; Type: {signal.AssetType}; SignalType: {signal.SignalType}; Score: {signal.Score.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}; Reason: {signal.Reason}; Action: {signal.Action}; Timeframe: {signal.Timeframe}; Confidence: {signal.Confidence}; Trend: {signal.Trend.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}; RSI: {FormatOptional(signal.Rsi)}; Drawdown: {FormatOptional(signal.Drawdown)}; Entry: {FormatOptional(signal.Entry)}; Stop: {FormatOptional(signal.Stop)}; Target: {FormatOptional(signal.Target)}; GeneratedAtUtc: {signal.GeneratedAtUtc:O}");
        }

        return promptBuilder.ToString();
    }

    private static string FormatOptional(decimal? value)
    {
        return value.HasValue
            ? value.Value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)
            : "n/a";
    }

    private static IReadOnlyCollection<MarketBriefingOpportunityItem> BuildTopOpportunities(
        IReadOnlyCollection<MarketSignal> signals)
    {
        return signals
            .Where(signal => signal.SignalType == MarketSignalType.Bullish && signal.Score >= 55m)
            .OrderByDescending(signal => signal.Score)
            .Take(5)
            .Select(signal => new MarketBriefingOpportunityItem(
                signal.Symbol,
                signal.Score,
                signal.Reason,
                signal.Entry,
                signal.Stop,
                signal.Target,
                signal.Action,
                signal.Timeframe,
                signal.Confidence))
            .ToArray();
    }

    private static IReadOnlyCollection<MarketBriefingPullbackItem> BuildWatchlistPullbacks(
        IReadOnlyCollection<MarketSignal> signals)
    {
        return signals
            .Where(signal => signal.Score >= 40m && signal.Score < 55m && IsPullbackSetup(signal))
            .OrderByDescending(signal => signal.Score)
            .Take(5)
            .Select(signal =>
            {
                var hasEnoughConfidence = signal.Score >= 50m;

                return new MarketBriefingPullbackItem(
                    signal.Symbol,
                    signal.Score,
                    signal.Reason,
                    hasEnoughConfidence ? signal.Entry : null,
                    hasEnoughConfidence ? signal.Stop : null,
                    hasEnoughConfidence ? signal.Target : null,
                    true,
                    "Watch for confirmation",
                    "WatchOnly",
                    "Low");
            })
            .ToArray();
    }

    private static IReadOnlyCollection<MarketBriefingRiskItem> BuildTopRisks(
        IReadOnlyCollection<MarketSignal> signals)
    {
        return signals
            .Where(signal => signal.Score < 40m)
            .OrderBy(signal => signal.Score)
            .Take(5)
            .Select(signal => new MarketBriefingRiskItem(
                signal.Symbol,
                signal.Score,
                signal.Reason,
                DetermineRiskType(signal),
                "Avoid / high risk",
                "WatchOnly",
                "Low"))
            .ToArray();
    }

    private static bool IsPullbackSetup(MarketSignal signal)
    {
        return signal.Reason.Contains("pullback near support", StringComparison.OrdinalIgnoreCase);
    }

    private static string DetermineRiskType(MarketSignal signal)
    {
        if (signal.Reason.Contains("intraday weakness", StringComparison.OrdinalIgnoreCase))
        {
            return "Intraday weakness";
        }

        if (signal.Reason.Contains("drawdown", StringComparison.OrdinalIgnoreCase))
        {
            return "Sharp drawdown";
        }

        if (signal.Reason.Contains("session low", StringComparison.OrdinalIgnoreCase))
        {
            return "Weak close";
        }

        return "Low signal score";
    }

    private static void AppendSignalSection(
        StringBuilder promptBuilder,
        string title,
        IReadOnlyCollection<MarketBriefingOpportunityItem> signals)
    {
        promptBuilder.AppendLine(title);

        if (signals.Count == 0)
        {
            promptBuilder.AppendLine("- none");
            return;
        }

        foreach (var signal in signals)
        {
            promptBuilder.AppendLine(
                $"- Symbol: {signal.Symbol}; Score: {signal.Score.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}; Reason: {signal.Reason}; Entry: {FormatOptional(signal.Entry)}; Stop: {FormatOptional(signal.Stop)}; Target: {FormatOptional(signal.Target)}; Action: {signal.Action}; Timeframe: {signal.Timeframe}; Confidence: {signal.Confidence}");
        }
    }

    private static void AppendSignalSection(
        StringBuilder promptBuilder,
        string title,
        IReadOnlyCollection<MarketBriefingPullbackItem> signals)
    {
        promptBuilder.AppendLine(title);

        if (signals.Count == 0)
        {
            promptBuilder.AppendLine("- none");
            return;
        }

        foreach (var signal in signals)
        {
            promptBuilder.AppendLine(
                $"- Symbol: {signal.Symbol}; Score: {signal.Score.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}; Reason: {signal.Reason}; Entry: {FormatOptional(signal.Entry)}; Stop: {FormatOptional(signal.Stop)}; Target: {FormatOptional(signal.Target)}; ConfirmationNeeded: {signal.ConfirmationNeeded}; Action: {signal.Action}; Timeframe: {signal.Timeframe}; Confidence: {signal.Confidence}");
        }
    }

    private static void AppendRiskSection(
        StringBuilder promptBuilder,
        string title,
        IReadOnlyCollection<MarketBriefingRiskItem> signals)
    {
        promptBuilder.AppendLine(title);

        if (signals.Count == 0)
        {
            promptBuilder.AppendLine("- none");
            return;
        }

        foreach (var signal in signals)
        {
            promptBuilder.AppendLine(
                $"- Symbol: {signal.Symbol}; Score: {signal.Score.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}; Reason: {signal.Reason}; RiskType: {signal.RiskType}; Action: {signal.Action}; Timeframe: {signal.Timeframe}; Confidence: {signal.Confidence}");
        }
    }

    private static ParsedBriefing ParseBriefing(string responseContent)
    {
        var json = ExtractJson(responseContent);
        var parsed = JsonSerializer.Deserialize<ParsedBriefing>(json, JsonSerializerOptions);

        if (parsed is null ||
            string.IsNullOrWhiteSpace(parsed.MarketRegime) ||
            string.IsNullOrWhiteSpace(parsed.Summary) ||
            string.IsNullOrWhiteSpace(parsed.SignalSummary))
        {
            throw new InvalidOperationException("The AI market briefing response could not be parsed.");
        }

        return parsed;
    }

    private static string ExtractJson(string responseContent)
    {
        var trimmed = responseContent.Trim();

        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var lines = trimmed.Split(
            new[] { "\r\n", "\n" },
            StringSplitOptions.None);

        var filtered = lines
            .Where(line => !line.StartsWith("```", StringComparison.Ordinal))
            .ToArray();

        return string.Join(Environment.NewLine, filtered).Trim();
    }

    private sealed record ParsedBriefing(
        string MarketRegime,
        string Summary,
        string SignalSummary,
        IReadOnlyCollection<string>? Highlights,
        IReadOnlyCollection<string>? Risks,
        IReadOnlyCollection<string>? WatchItems);
}
