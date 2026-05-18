using System.Text;
using System.Text.Json;
using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Models;
using MarketAgent.Domain.Entities;
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
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshots);

        if (snapshots.Count == 0)
        {
            throw new InvalidOperationException("At least one market snapshot is required to generate a briefing.");
        }

        var chatCompletionService = CreateChatCompletionService();
        var prompt = BuildPrompt(snapshots);

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

        return new MarketBriefingResult(
            DateTime.UtcNow,
            briefing.Summary,
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

    private static string BuildPrompt(IReadOnlyCollection<MarketSnapshot> snapshots)
    {
        var promptBuilder = new StringBuilder();

        promptBuilder.AppendLine("You are Market Agent.");
        promptBuilder.AppendLine("Summarize only the market snapshots provided below.");
        promptBuilder.AppendLine("Do not invent data.");
        promptBuilder.AppendLine("Do not give trading advice, buy/sell recommendations, or unsupported claims.");
        promptBuilder.AppendLine("Return valid JSON only with this shape:");
        promptBuilder.AppendLine("""{"summary":"string","highlights":["string"],"risks":["string"],"watchItems":["string"]}""");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Snapshots:");

        foreach (var snapshot in snapshots.OrderByDescending(item => item.CapturedAtUtc))
        {
            promptBuilder.AppendLine(
                $"- Symbol: {snapshot.Symbol}; Type: {snapshot.AssetType}; Price: {snapshot.Price.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)}; Currency: {snapshot.Currency}; CapturedAtUtc: {snapshot.CapturedAtUtc:O}; Source: {snapshot.Source}; Open: {FormatOptional(snapshot.OpenPrice)}; High: {FormatOptional(snapshot.HighPrice)}; Low: {FormatOptional(snapshot.LowPrice)}; PreviousClose: {FormatOptional(snapshot.PreviousClose)}; Volume: {FormatOptional(snapshot.Volume)}");
        }

        return promptBuilder.ToString();
    }

    private static string FormatOptional(decimal? value)
    {
        return value.HasValue
            ? value.Value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)
            : "n/a";
    }

    private static ParsedBriefing ParseBriefing(string responseContent)
    {
        var json = ExtractJson(responseContent);
        var parsed = JsonSerializer.Deserialize<ParsedBriefing>(json, JsonSerializerOptions);

        if (parsed is null || string.IsNullOrWhiteSpace(parsed.Summary))
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
        string Summary,
        IReadOnlyCollection<string>? Highlights,
        IReadOnlyCollection<string>? Risks,
        IReadOnlyCollection<string>? WatchItems);
}
