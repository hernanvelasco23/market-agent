using System.Globalization;
using System.Text.Json;
using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Models;
using MarketAgent.Domain.Entities;
using MarketAgent.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MarketAgent.Infrastructure.MarketData;

public sealed class HistoricalMarketDataProvider : IHistoricalMarketDataProvider
{
    private const string YahooSource = "YahooFinance";
    private const string StooqSource = "Stooq";
    private static readonly Uri YahooBaseAddress = new("https://query1.finance.yahoo.com/");
    private static readonly Uri StooqBaseAddress = new("https://stooq.com/");

    private static readonly IReadOnlyDictionary<string, string> YahooProviderSymbols =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["MU"] = "MU",
            ["AMZN"] = "AMZN",
            ["V"] = "V",
            ["ASTS"] = "ASTS",
            ["NKE"] = "NKE",
            ["PLTR"] = "PLTR",
            ["PATH"] = "PATH",
            ["META"] = "META",
            ["GOOG"] = "GOOG",
            ["RKLB"] = "RKLB",
            ["NVDA"] = "NVDA",
            ["MSFT"] = "MSFT",
            ["AMD"] = "AMD",
            ["SPY"] = "SPY",
            ["MELI"] = "MELI",
            ["TSLA"] = "TSLA",
            ["NU"] = "NU",
            ["BTC"] = "BTC-USD",
            ["ETH"] = "ETH-USD"
        };

    private static readonly IReadOnlyDictionary<string, string> StooqProviderSymbols =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["MU"] = "mu.us",
            ["AMZN"] = "amzn.us",
            ["V"] = "v.us",
            ["ASTS"] = "asts.us",
            ["NKE"] = "nke.us",
            ["PLTR"] = "pltr.us",
            ["PATH"] = "path.us",
            ["META"] = "meta.us",
            ["GOOG"] = "goog.us",
            ["RKLB"] = "rklb.us",
            ["NVDA"] = "nvda.us",
            ["MSFT"] = "msft.us",
            ["AMD"] = "amd.us",
            ["SPY"] = "spy.us",
            ["MELI"] = "meli.us",
            ["TSLA"] = "tsla.us",
            ["NU"] = "nu.us"
        };

    private readonly HistoricalMarketDataOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ILogger<HistoricalMarketDataProvider> _logger;

    public HistoricalMarketDataProvider(
        HttpClient httpClient,
        IOptions<HistoricalMarketDataOptions> options,
        ILogger<HistoricalMarketDataProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MarketAgent/1.0");
        }
    }

    public bool CanHandle(TrackedAsset asset)
    {
        var symbol = NormalizeSymbol(asset.Symbol);

        return asset.AssetType switch
        {
            AssetType.Equity or AssetType.Etf or AssetType.Crypto => YahooProviderSymbols.ContainsKey(symbol),
            _ => false
        };
    }

    public Task<IReadOnlyCollection<MarketCandle>> GetDailyCandlesAsync(
        TrackedAsset asset,
        int days,
        CancellationToken cancellationToken = default)
    {
        var symbol = NormalizeSymbol(asset.Symbol);
        var requestedDays = Math.Clamp(days, 1, 300);

        return asset.AssetType switch
        {
            AssetType.Equity or AssetType.Etf or AssetType.Crypto => GetCandlesWithFallbackAsync(
                asset,
                symbol,
                requestedDays,
                cancellationToken),
            _ => Task.FromResult<IReadOnlyCollection<MarketCandle>>([])
        };
    }

    private async Task<IReadOnlyCollection<MarketCandle>> GetCandlesWithFallbackAsync(
        TrackedAsset asset,
        string symbol,
        int days,
        CancellationToken cancellationToken)
    {
        var failures = new List<string>();

        try
        {
            return await GetYahooDailyCandlesAsync(asset, symbol, days, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            failures.Add(exception.Message);
        }

        if (asset.AssetType is AssetType.Equity or AssetType.Etf &&
            IsStooqConfigured())
        {
            try
            {
                return await GetStooqDailyCandlesAsync(asset, symbol, days, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                failures.Add(exception.Message);
            }
        }
        else if (asset.AssetType is AssetType.Equity or AssetType.Etf)
        {
            failures.Add($"providerName={StooqSource}; originalSymbol={symbol}; rawErrorSummary=Stooq historical provider skipped because no API key is configured.");
        }

        throw new InvalidOperationException(
            $"originalSymbol={symbol}; rawErrorSummary=all historical providers failed; failures={string.Join(" | ", failures)}");
    }

    private async Task<IReadOnlyCollection<MarketCandle>> GetYahooDailyCandlesAsync(
        TrackedAsset asset,
        string symbol,
        int days,
        CancellationToken cancellationToken)
    {
        var providerSymbol = MapToYahooProviderSymbol(symbol);
        var requestUri = new Uri(
            YahooBaseAddress,
            $"v8/finance/chart/{Uri.EscapeDataString(providerSymbol)}?range={days}d&interval=1d");

        _logger.LogInformation(
            "Requesting historical market data. providerName={ProviderName}; originalSymbol={OriginalSymbol}; providerSymbol={ProviderSymbol}; requestUrl={RequestUrl}",
            YahooSource,
            symbol,
            providerSymbol,
            requestUri);

        using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"providerName={YahooSource}; originalSymbol={symbol}; providerSymbol={providerSymbol}; requestUrl={requestUri}; rawErrorSummary=HTTP {(int)response.StatusCode} {response.ReasonPhrase}; rawResponsePreview={BuildRawResponsePreview(content)}");
        }

        return ParseYahooCandles(content, asset, symbol, providerSymbol, requestUri)
            .TakeLast(days)
            .ToArray();
    }

    private async Task<IReadOnlyCollection<MarketCandle>> GetStooqDailyCandlesAsync(
        TrackedAsset asset,
        string symbol,
        int days,
        CancellationToken cancellationToken)
    {
        var providerSymbol = MapToStooqProviderSymbol(symbol);
        var endDate = DateTime.UtcNow.Date;
        var startDate = endDate.AddDays(-(days * 2));
        var requestUri = BuildStooqRequestUri(providerSymbol, startDate, endDate, includeApiKey: true);
        var safeRequestUri = BuildStooqRequestUri(providerSymbol, startDate, endDate, includeApiKey: false);

        _logger.LogInformation(
            "Requesting historical market data. providerName={ProviderName}; originalSymbol={OriginalSymbol}; providerSymbol={ProviderSymbol}; requestUrl={RequestUrl}",
            StooqSource,
            symbol,
            providerSymbol,
            safeRequestUri);

        using var response = await _httpClient.GetAsync(requestUri, cancellationToken);

        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"providerName={StooqSource}; originalSymbol={symbol}; providerSymbol={providerSymbol}; requestUrl={safeRequestUri}; rawErrorSummary=HTTP {(int)response.StatusCode} {response.ReasonPhrase}; rawResponsePreview={BuildRawResponsePreview(content)}");
        }

        return ParseStooqCandles(content, asset, symbol, providerSymbol, safeRequestUri)
            .TakeLast(days)
            .ToArray();
    }

    private Uri BuildStooqRequestUri(
        string providerSymbol,
        DateTime startDate,
        DateTime endDate,
        bool includeApiKey)
    {
        var apiKey = _options.StooqApiKey?.Trim();
        var apiKeyParameter = includeApiKey && !string.IsNullOrWhiteSpace(apiKey)
            ? $"&apikey={Uri.EscapeDataString(apiKey)}"
            : string.Empty;

        return new Uri(
            StooqBaseAddress,
            $"q/d/l/?s={providerSymbol}&d1={startDate:yyyyMMdd}&d2={endDate:yyyyMMdd}&i=d{apiKeyParameter}");
    }

    private IReadOnlyCollection<MarketCandle> ParseStooqCandles(
        string content,
        TrackedAsset asset,
        string symbol,
        string providerSymbol,
        Uri requestUri)
    {
        var rows = content.Split(
            new[] { '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (rows.Length < 2)
        {
            throw new InvalidOperationException(
                $"providerName={StooqSource}; originalSymbol={symbol}; providerSymbol={providerSymbol}; requestUrl={requestUri}; rawErrorSummary=historical market data response did not contain candle data; rawResponsePreview={BuildRawResponsePreview(content)}");
        }

        var header = SplitCsvRow(rows[0]);
        var format = ResolveStooqCandleFormat(header);
        var candles = new List<MarketCandle>();
        var skippedRows = 0;
        var lastSkippedReason = string.Empty;

        foreach (var row in rows.Skip(1))
        {
            if (TryParseStooqCandle(row, asset, symbol, format, out var candle, out var skipReason))
            {
                candles.Add(candle);
                continue;
            }

            skippedRows++;
            lastSkippedReason = skipReason;
        }

        if (candles.Count == 0)
        {
            throw new InvalidOperationException(
                $"providerName={StooqSource}; originalSymbol={symbol}; providerSymbol={providerSymbol}; requestUrl={requestUri}; parsed zero valid historical candles; skippedRows={skippedRows}; rawErrorSummary={lastSkippedReason}; rawResponsePreview={BuildRawResponsePreview(content)}");
        }

        if (skippedRows > 0)
        {
            _logger.LogWarning(
                "Skipped invalid historical candle rows. providerName={ProviderName}; originalSymbol={OriginalSymbol}; providerSymbol={ProviderSymbol}; requestUrl={RequestUrl}; skippedRows={SkippedRows}; lastSkippedReason={Reason}",
                StooqSource,
                symbol,
                providerSymbol,
                requestUri,
                skippedRows,
                lastSkippedReason);
        }

        return candles
            .OrderBy(candle => candle.OccurredAtUtc)
            .ToArray();
    }

    private IReadOnlyCollection<MarketCandle> ParseYahooCandles(
        string content,
        TrackedAsset asset,
        string symbol,
        string providerSymbol,
        Uri requestUri)
    {
        using var document = JsonDocument.Parse(content);
        var result = document.RootElement
            .GetProperty("chart")
            .GetProperty("result");

        if (result.GetArrayLength() == 0)
        {
            throw new InvalidOperationException(
                $"providerName={YahooSource}; originalSymbol={symbol}; providerSymbol={providerSymbol}; requestUrl={requestUri}; rawErrorSummary=Yahoo response contained no chart result; rawResponsePreview={BuildRawResponsePreview(content)}");
        }

        var chart = result[0];

        if (!chart.TryGetProperty("timestamp", out var timestamps) ||
            !chart.TryGetProperty("indicators", out var indicators) ||
            !indicators.TryGetProperty("quote", out var quotes) ||
            quotes.GetArrayLength() == 0)
        {
            throw new InvalidOperationException(
                $"providerName={YahooSource}; originalSymbol={symbol}; providerSymbol={providerSymbol}; requestUrl={requestUri}; rawErrorSummary=Yahoo response was missing timestamp or quote arrays; rawResponsePreview={BuildRawResponsePreview(content)}");
        }

        var quote = quotes[0];
        var opens = quote.GetProperty("open");
        var highs = quote.GetProperty("high");
        var lows = quote.GetProperty("low");
        var closes = quote.GetProperty("close");
        var volumes = quote.TryGetProperty("volume", out var volumeArray)
            ? volumeArray
            : default;
        var rowCount = new[]
        {
            timestamps.GetArrayLength(),
            opens.GetArrayLength(),
            highs.GetArrayLength(),
            lows.GetArrayLength(),
            closes.GetArrayLength()
        }.Min();
        var candles = new List<MarketCandle>();
        var skippedRows = 0;

        for (var index = 0; index < rowCount; index++)
        {
            if (!TryGetUnixTimestamp(timestamps[index], out var timestamp) ||
                !TryGetDecimal(opens[index], out var open) ||
                !TryGetDecimal(highs[index], out var high) ||
                !TryGetDecimal(lows[index], out var low) ||
                !TryGetDecimal(closes[index], out var close) ||
                high < low)
            {
                skippedRows++;
                continue;
            }

            var volume = TryGetVolume(volumes, index, out var parsedVolume)
                ? parsedVolume
                : 0m;

            candles.Add(new MarketCandle(
                symbol,
                asset.AssetType,
                DateTime.SpecifyKind(DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime.Date, DateTimeKind.Utc),
                open,
                high,
                low,
                close,
                volume,
                YahooSource));
        }

        if (candles.Count == 0)
        {
            throw new InvalidOperationException(
                $"providerName={YahooSource}; originalSymbol={symbol}; providerSymbol={providerSymbol}; requestUrl={requestUri}; skippedRows={skippedRows}; validCandles=0; rawErrorSummary=Yahoo parsed zero valid historical candles; rawResponsePreview={BuildRawResponsePreview(content)}");
        }

        _logger.LogInformation(
            "Parsed historical candles. providerName={ProviderName}; originalSymbol={OriginalSymbol}; providerSymbol={ProviderSymbol}; skippedRows={SkippedRows}; validCandles={ValidCandles}",
            YahooSource,
            symbol,
            providerSymbol,
            skippedRows,
            candles.Count);

        return candles
            .OrderBy(candle => candle.OccurredAtUtc)
            .ToArray();
    }

    private static bool TryParseStooqCandle(
        string row,
        TrackedAsset asset,
        string symbol,
        StooqCandleFormat format,
        out MarketCandle candle,
        out string skipReason)
    {
        candle = null!;
        skipReason = string.Empty;

        if (string.IsNullOrWhiteSpace(row))
        {
            skipReason = "empty row";
            return false;
        }

        var fields = SplitCsvRow(row);

        if (fields.Length <= format.RequiredMaxIndex)
        {
            skipReason = $"incomplete row with {fields.Length} columns";
            return false;
        }

        if (!TryParseStooqDate(fields[format.DateIndex], out var occurredAtUtc))
        {
            skipReason = "invalid date";
            return false;
        }

        if (!TryParseRequiredDecimal(fields[format.OpenIndex], out var open) ||
            !TryParseRequiredDecimal(fields[format.HighIndex], out var high) ||
            !TryParseRequiredDecimal(fields[format.LowIndex], out var low) ||
            !TryParseRequiredDecimal(fields[format.CloseIndex], out var close) ||
            !TryParseRequiredDecimal(fields[format.VolumeIndex], out var volume))
        {
            skipReason = "missing or invalid OHLCV value";
            return false;
        }

        if (high < low)
        {
            skipReason = "high lower than low";
            return false;
        }

        candle = new MarketCandle(
            symbol,
            asset.AssetType,
            occurredAtUtc,
            open,
            high,
            low,
            close,
            volume,
            StooqSource);

        return true;
    }

    private static string NormalizeSymbol(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Asset symbol is required.", nameof(symbol));
        }

        return symbol.Trim().ToUpperInvariant();
    }

    private static string MapToStooqProviderSymbol(string symbol)
    {
        var normalizedSymbol = NormalizeSymbol(symbol);

        if (StooqProviderSymbols.TryGetValue(normalizedSymbol, out var providerSymbol))
        {
            return NormalizeStooqProviderSymbol(providerSymbol);
        }

        throw new NotSupportedException(
            $"providerName={StooqSource}; originalSymbol={symbol}; rawErrorSummary=symbol is not supported by {nameof(HistoricalMarketDataProvider)}.");
    }

    private static string NormalizeStooqProviderSymbol(string providerSymbol)
    {
        var normalized = providerSymbol.Trim().ToLowerInvariant();

        return normalized.EndsWith(".us", StringComparison.OrdinalIgnoreCase)
            ? normalized
            : $"{normalized}.us";
    }

    private static string MapToYahooProviderSymbol(string symbol)
    {
        var normalizedSymbol = NormalizeSymbol(symbol);

        if (YahooProviderSymbols.TryGetValue(normalizedSymbol, out var providerSymbol))
        {
            return providerSymbol;
        }

        throw new NotSupportedException(
            $"providerName={YahooSource}; originalSymbol={symbol}; rawErrorSummary=symbol is not supported by {nameof(HistoricalMarketDataProvider)}.");
    }

    private bool IsStooqConfigured()
    {
        return !string.IsNullOrWhiteSpace(_options.StooqApiKey);
    }

    private static string[] SplitCsvRow(string row)
    {
        var separator = row.Contains(';', StringComparison.Ordinal) ? ';' : ',';

        return row.Split(
            new[] { separator },
            StringSplitOptions.TrimEntries);
    }

    private static StooqCandleFormat ResolveStooqCandleFormat(IReadOnlyList<string> header)
    {
        var indexes = header
            .Select((value, index) => new { Name = NormalizeHeader(value), Index = index })
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .ToDictionary(item => item.Name, item => item.Index, StringComparer.OrdinalIgnoreCase);

        return new StooqCandleFormat(
            GetIndex(indexes, "date", 0),
            GetIndex(indexes, "open", 1),
            GetIndex(indexes, "high", 2),
            GetIndex(indexes, "low", 3),
            GetIndex(indexes, "close", 4),
            ResolveVolumeIndex(indexes, header.Count));
    }

    private static int GetIndex(
        IReadOnlyDictionary<string, int> indexes,
        string name,
        int fallback)
    {
        return indexes.TryGetValue(name, out var index)
            ? index
            : fallback;
    }

    private static int ResolveVolumeIndex(
        IReadOnlyDictionary<string, int> indexes,
        int columnCount)
    {
        if (indexes.TryGetValue("volume", out var volumeIndex))
        {
            return volumeIndex;
        }

        return columnCount >= 7 ? 6 : 5;
    }

    private static string NormalizeHeader(string value)
    {
        return value
            .Trim()
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();
    }

    private static bool TryParseStooqDate(string value, out DateTime occurredAtUtc)
    {
        if (DateTime.TryParseExact(
                value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out occurredAtUtc))
        {
            occurredAtUtc = DateTime.SpecifyKind(occurredAtUtc.Date, DateTimeKind.Utc);
            return true;
        }

        return false;
    }

    private static bool TryParseRequiredDecimal(string value, out decimal result)
    {
        result = 0m;

        if (string.IsNullOrWhiteSpace(value) ||
            value.Equals("null", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("N/D", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("NaN", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result) ||
            decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out result))
        {
            return result > 0;
        }

        var normalized = value.Trim().Replace(',', '.');

        return decimal.TryParse(
                normalized,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out result) &&
            result > 0;
    }

    private static bool TryGetUnixTimestamp(JsonElement value, out long timestamp)
    {
        timestamp = 0;

        return value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt64(out timestamp);
    }

    private static bool TryGetDecimal(JsonElement value, out decimal result)
    {
        result = 0m;

        return value.ValueKind == JsonValueKind.Number &&
            value.TryGetDecimal(out result) &&
            result > 0;
    }

    private static bool TryGetVolume(JsonElement volumes, int index, out decimal volume)
    {
        volume = 0m;

        if (volumes.ValueKind != JsonValueKind.Array ||
            index >= volumes.GetArrayLength())
        {
            return false;
        }

        var value = volumes[index];

        if (value.ValueKind == JsonValueKind.Null)
        {
            return false;
        }

        return value.ValueKind == JsonValueKind.Number &&
            value.TryGetDecimal(out volume) &&
            volume >= 0;
    }

    private static string BuildRawResponsePreview(string content)
    {
        const int maxLength = 240;

        if (string.IsNullOrWhiteSpace(content))
        {
            return "<empty>";
        }

        var normalized = content
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Trim();

        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength];
    }

    private sealed record StooqCandleFormat(
        int DateIndex,
        int OpenIndex,
        int HighIndex,
        int LowIndex,
        int CloseIndex,
        int VolumeIndex)
    {
        public int RequiredMaxIndex => new[]
        {
            DateIndex,
            OpenIndex,
            HighIndex,
            LowIndex,
            CloseIndex,
            VolumeIndex
        }.Max();
    }
}
