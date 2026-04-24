using System.Globalization;
using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Models;
using MarketAgent.Domain.Enums;

namespace MarketAgent.Infrastructure.MarketData;

public sealed class EquityMarketDataProvider : IMarketDataProvider
{
    private const string Source = "Stooq";
    private static readonly Uri BaseAddress = new("https://stooq.com/");

    private static readonly IReadOnlyDictionary<string, string> ProviderSymbols =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["NVDA"] = "nvda.us",
            ["MSFT"] = "msft.us",
            ["AMD"] = "amd.us",
            ["SPY"] = "spy.us",
            ["MELI"] = "meli.us",
            ["TSLA"] = "tsla.us",
            ["NU"] = "nu.us"
        };

    private readonly HttpClient _httpClient;

    public EquityMarketDataProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress ??= BaseAddress;
    }

    public bool CanHandle(TrackedAsset asset)
    {
        if (asset.AssetType is not (AssetType.Equity or AssetType.Etf))
        {
            return false;
        }

        var symbol = NormalizeSymbol(asset.Symbol);
        return ProviderSymbols.ContainsKey(symbol);
    }

    public async Task<MarketDataResult> GetLatestAsync(
        TrackedAsset asset,
        CancellationToken cancellationToken = default)
    {
        ValidateAssetType(asset.AssetType);

        var symbol = NormalizeSymbol(asset.Symbol);
        var providerSymbol = MapToProviderSymbol(symbol);
        var requestUri = BuildQuoteRequestUri(providerSymbol);

        using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var quote = ParseQuote(content, symbol);

        return MapToMarketDataResult(asset, symbol, quote);
    }

    private static void ValidateAssetType(AssetType assetType)
    {
        if (assetType is AssetType.Equity or AssetType.Etf)
        {
            return;
        }

        throw new NotSupportedException(
            $"{nameof(EquityMarketDataProvider)} supports only equity and ETF assets.");
    }

    private static string NormalizeSymbol(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Asset symbol is required.", nameof(symbol));
        }

        return symbol.Trim().ToUpperInvariant();
    }

    private static string MapToProviderSymbol(string symbol)
    {
        if (ProviderSymbols.TryGetValue(symbol, out var providerSymbol))
        {
            return providerSymbol;
        }

        throw new NotSupportedException(
            $"Symbol '{symbol}' is not supported by {nameof(EquityMarketDataProvider)}.");
    }

    private static string BuildQuoteRequestUri(string providerSymbol)
    {
        return $"q/l/?s={providerSymbol}&f=sd2t2ohlcv&h&e=csv";
    }

    private static StooqQuote ParseQuote(string content, string symbol)
    {
        var rows = SplitRows(content);

        if (rows.Length < StooqCsvFormat.MinimumRowCount)
        {
            throw new InvalidOperationException(
                $"Market data response for '{symbol}' did not contain quote data.");
        }

        var fields = SplitFields(rows[StooqCsvFormat.DataRowIndex]);

        if (fields.Length < StooqCsvFormat.ExpectedFieldCount || HasMissingProviderValue(fields))
        {
            throw new InvalidOperationException(
                $"Market data response for '{symbol}' was incomplete.");
        }

        return new StooqQuote(
            ParseRequiredDecimal(fields[StooqCsvFormat.OpenIndex], symbol, "open"),
            ParseRequiredDecimal(fields[StooqCsvFormat.HighIndex], symbol, "high"),
            ParseRequiredDecimal(fields[StooqCsvFormat.LowIndex], symbol, "low"),
            ParseRequiredDecimal(fields[StooqCsvFormat.CloseIndex], symbol, "close"),
            ParseOptionalDecimal(fields[StooqCsvFormat.VolumeIndex], symbol, "volume"),
            ParseCapturedAtUtc(
                fields[StooqCsvFormat.DateIndex],
                fields[StooqCsvFormat.TimeIndex],
                symbol));
    }

    private static string[] SplitRows(string content)
    {
        return content.Split(
            new[] { '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string[] SplitFields(string row)
    {
        return row.Split(
            new[] { ',' },
            StringSplitOptions.TrimEntries);
    }

    private static bool HasMissingProviderValue(IEnumerable<string> fields)
    {
        return fields.Any(field => field.Equals("N/D", StringComparison.OrdinalIgnoreCase));
    }

    private static DateTime ParseCapturedAtUtc(string date, string time, string symbol)
    {
        var value = $"{date} {time}";

        if (!DateTime.TryParseExact(
                value,
                StooqCsvFormat.DateTimeFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var capturedAtUtc))
        {
            throw new InvalidOperationException(
                $"Market data response for '{symbol}' had an invalid timestamp.");
        }

        return DateTime.SpecifyKind(capturedAtUtc, DateTimeKind.Utc);
    }

    private static decimal ParseRequiredDecimal(string value, string symbol, string fieldName)
    {
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        throw new InvalidOperationException(
            $"Market data response for '{symbol}' had an invalid {fieldName} value.");
    }

    private static decimal? ParseOptionalDecimal(string value, string symbol, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return ParseRequiredDecimal(value, symbol, fieldName);
    }

    private static MarketDataResult MapToMarketDataResult(
        TrackedAsset asset,
        string symbol,
        StooqQuote quote)
    {
        return new MarketDataResult(
            symbol,
            asset.AssetType,
            quote.Close,
            asset.Currency,
            quote.CapturedAtUtc,
            Source,
            quote.Volume,
            quote.Open,
            quote.High,
            quote.Low);
    }

    private sealed record StooqQuote(
        decimal Open,
        decimal High,
        decimal Low,
        decimal Close,
        decimal? Volume,
        DateTime CapturedAtUtc);

    private static class StooqCsvFormat
    {
        public const int MinimumRowCount = 2;
        public const int DataRowIndex = 1;
        public const int ExpectedFieldCount = 8;
        public const int DateIndex = 1;
        public const int TimeIndex = 2;
        public const int OpenIndex = 3;
        public const int HighIndex = 4;
        public const int LowIndex = 5;
        public const int CloseIndex = 6;
        public const int VolumeIndex = 7;
        public const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";
    }
}
