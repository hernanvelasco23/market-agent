using System.Globalization;
using System.Text.Json;
using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Models;
using MarketAgent.Domain.Enums;

namespace MarketAgent.Infrastructure.MarketData;

public sealed class EquityMarketDataProvider : IMarketDataProvider
{
    private const string YahooSource = "YahooFinance";
    private const string StooqSource = "Stooq";
    private static readonly Uri StooqBaseAddress = new("https://stooq.com/");

    private static readonly IReadOnlyDictionary<string, string> YahooProviderSymbols =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["MU"] = "MU",
            ["AMZN"] = "AMZN",
            ["AXP"] = "AXP",
            ["BRK.B"] = "BRK-B",
            ["V"] = "V",
            ["ASTS"] = "ASTS",
            ["NKE"] = "NKE",
            ["PLTR"] = "PLTR",
            ["PATH"] = "PATH",
            ["IBM"] = "IBM",
            ["META"] = "META",
            ["GOOG"] = "GOOG",
            ["GOOGL"] = "GOOGL",
            ["ORCL"] = "ORCL",
            ["RKLB"] = "RKLB",
            ["RGTI"] = "RGTI",
            ["SE"] = "SE",
            ["NVDA"] = "NVDA",
            ["MSFT"] = "MSFT",
            ["AAPL"] = "AAPL",
            ["AMD"] = "AMD",
            ["SPY"] = "SPY",
            ["MELI"] = "MELI",
            ["TSLA"] = "TSLA",
            ["NU"] = "NU",
            ["GGAL"] = "GGAL",
            ["YPF"] = "YPF",
            ["BMA"] = "BMA",
            ["PAM"] = "PAM",
            ["TGS"] = "TGS",
            ["VIST"] = "VIST",
            ["PBR"] = "PBR",
            ["VALE"] = "VALE",
            ["BBD"] = "BBD",
            ["ITUB"] = "ITUB",
            ["JPM"] = "JPM",
            ["BAC"] = "BAC",
            ["GS"] = "GS",
            ["XOM"] = "XOM",
            ["CVX"] = "CVX",
            ["TTE"] = "TTE",
            ["NIO"] = "NIO",
            ["PYPL"] = "PYPL",
            ["SHOP"] = "SHOP",
            ["COST"] = "COST",
            ["MSTR"] = "MSTR",
            ["COIN"] = "COIN",
            ["IBIT"] = "IBIT",
            ["ETHA"] = "ETHA",
            ["KO"] = "KO",
            ["PEP"] = "PEP",
            ["PG"] = "PG",
            ["WMT"] = "WMT",
            ["DIS"] = "DIS",
            ["NFLX"] = "NFLX",
            ["ASML"] = "ASML",
            ["TSM"] = "TSM",
            ["AVGO"] = "AVGO",
            ["QCOM"] = "QCOM",
            ["MRVL"] = "MRVL",
            ["PDD"] = "PDD",
            ["TM"] = "TM",
            ["UBER"] = "UBER",
            ["CVS"] = "CVS",
            ["GLOB"] = "GLOB",
            ["QQQ"] = "QQQ"
        };

    private static readonly IReadOnlyDictionary<string, string> StooqProviderSymbols =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["MU"] = "mu.us",
            ["AMZN"] = "amzn.us",
            ["AXP"] = "axp.us",
            ["BRK.B"] = "brk.b.us",
            ["V"] = "v.us",
            ["ASTS"] = "asts.us",
            ["NKE"] = "nke.us",
            ["PLTR"] = "pltr.us",
            ["PATH"] = "path.us",
            ["IBM"] = "ibm.us",
            ["META"] = "meta.us",
            ["GOOG"] = "goog.us",
            ["GOOGL"] = "googl.us",
            ["ORCL"] = "orcl.us",
            ["RKLB"] = "rklb.us",
            ["RGTI"] = "rgti.us",
            ["SE"] = "se.us",
            ["NVDA"] = "nvda.us",
            ["MSFT"] = "msft.us",
            ["AAPL"] = "aapl.us",
            ["AMD"] = "amd.us",
            ["SPY"] = "spy.us",
            ["MELI"] = "meli.us",
            ["TSLA"] = "tsla.us",
            ["NU"] = "nu.us",
            ["GGAL"] = "ggal.us",
            ["YPF"] = "ypf.us",
            ["BMA"] = "bma.us",
            ["PAM"] = "pam.us",
            ["TGS"] = "tgs.us",
            ["VIST"] = "vist.us",
            ["PBR"] = "pbr.us",
            ["VALE"] = "vale.us",
            ["BBD"] = "bbd.us",
            ["ITUB"] = "itub.us",
            ["JPM"] = "jpm.us",
            ["BAC"] = "bac.us",
            ["GS"] = "gs.us",
            ["XOM"] = "xom.us",
            ["CVX"] = "cvx.us",
            ["TTE"] = "tte.us",
            ["NIO"] = "nio.us",
            ["PYPL"] = "pypl.us",
            ["SHOP"] = "shop.us",
            ["COST"] = "cost.us",
            ["MSTR"] = "mstr.us",
            ["COIN"] = "coin.us",
            ["IBIT"] = "ibit.us",
            ["ETHA"] = "etha.us",
            ["KO"] = "ko.us",
            ["PEP"] = "pep.us",
            ["PG"] = "pg.us",
            ["WMT"] = "wmt.us",
            ["DIS"] = "dis.us",
            ["NFLX"] = "nflx.us",
            ["ASML"] = "asml.us",
            ["TSM"] = "tsm.us",
            ["AVGO"] = "avgo.us",
            ["QCOM"] = "qcom.us",
            ["MRVL"] = "mrvl.us",
            ["PDD"] = "pdd.us",
            ["TM"] = "tm.us",
            ["UBER"] = "uber.us",
            ["CVS"] = "cvs.us",
            ["GLOB"] = "glob.us",
            ["QQQ"] = "qqq.us"
        };

    private readonly HttpClient _httpClient;

    public EquityMarketDataProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress ??= StooqBaseAddress;
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MarketAgent/1.0");
        }
    }

    public bool CanHandle(TrackedAsset asset)
    {
        if (asset.AssetType is not (AssetType.Equity or AssetType.Etf))
        {
            return false;
        }

        var symbol = NormalizeSymbol(asset.Symbol);
        return YahooProviderSymbols.ContainsKey(symbol) || StooqProviderSymbols.ContainsKey(symbol);
    }

    public async Task<MarketDataResult> GetLatestAsync(
        TrackedAsset asset,
        CancellationToken cancellationToken = default)
    {
        ValidateAssetType(asset.AssetType);

        var symbol = NormalizeSymbol(asset.Symbol);
        if (YahooProviderSymbols.TryGetValue(symbol, out var yahooProviderSymbol))
        {
            try
            {
                return await GetYahooLatestAsync(asset, symbol, yahooProviderSymbol, cancellationToken);
            }
            catch when (StooqProviderSymbols.ContainsKey(symbol))
            {
                // Stooq is delayed, but it is better than failing the whole ingestion if Yahoo is unavailable.
            }
        }

        return await GetStooqLatestAsync(asset, symbol, cancellationToken);
    }

    private async Task<MarketDataResult> GetYahooLatestAsync(
        TrackedAsset asset,
        string symbol,
        string providerSymbol,
        CancellationToken cancellationToken)
    {
        var requestUri = BuildYahooChartRequestUri(providerSymbol);

        using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var quote = ParseYahooQuote(content, symbol);

        return new MarketDataResult(
            symbol,
            asset.AssetType,
            quote.Price,
            quote.Currency ?? asset.Currency,
            quote.CapturedAtUtc,
            YahooSource,
            quote.Volume,
            quote.Open,
            quote.High,
            quote.Low,
            quote.PreviousClose);
    }

    private async Task<MarketDataResult> GetStooqLatestAsync(
        TrackedAsset asset,
        string symbol,
        CancellationToken cancellationToken)
    {
        var providerSymbol = MapToStooqProviderSymbol(symbol);
        var requestUri = BuildStooqQuoteRequestUri(providerSymbol);

        using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var quote = ParseStooqQuote(content, symbol);

        return MapStooqToMarketDataResult(asset, symbol, quote);
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

    private static string MapToStooqProviderSymbol(string symbol)
    {
        if (StooqProviderSymbols.TryGetValue(symbol, out var providerSymbol))
        {
            return providerSymbol;
        }

        throw new NotSupportedException(
            $"Symbol '{symbol}' is not supported by {nameof(EquityMarketDataProvider)}.");
    }

    private static string BuildYahooChartRequestUri(string providerSymbol)
    {
        return $"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(providerSymbol)}?range=1d&interval=1m&includePrePost=true&cb={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
    }

    private static string BuildStooqQuoteRequestUri(string providerSymbol)
    {
        return $"q/l/?s={providerSymbol}&f=sd2t2ohlcv&h&e=csv&cb={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
    }

    private static YahooQuote ParseYahooQuote(string content, string symbol)
    {
        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;
        var result = root
            .GetProperty("chart")
            .GetProperty("result");

        if (result.ValueKind != JsonValueKind.Array || result.GetArrayLength() == 0)
        {
            throw new InvalidOperationException(
                $"Yahoo market data response for '{symbol}' did not contain quote data.");
        }

        var item = result[0];
        var meta = item.GetProperty("meta");
        var regularMarketPrice = GetOptionalDecimal(meta, "regularMarketPrice");
        var regularMarketTime = GetOptionalUnixTime(meta, "regularMarketTime");
        var previousClose = GetOptionalDecimal(meta, "previousClose") ??
            GetOptionalDecimal(meta, "chartPreviousClose");
        var currency = meta.TryGetProperty("currency", out var currencyElement)
            ? currencyElement.GetString()
            : null;

        var latestCandle = TryGetLatestYahooCandle(item);
        var price = regularMarketPrice ?? latestCandle?.Close;
        var capturedAtUtc = regularMarketTime ?? latestCandle?.CapturedAtUtc;

        if (price is null || capturedAtUtc is null)
        {
            throw new InvalidOperationException(
                $"Yahoo market data response for '{symbol}' was incomplete.");
        }

        return new YahooQuote(
            price.Value,
            currency,
            capturedAtUtc.Value,
            latestCandle?.Open,
            latestCandle?.High,
            latestCandle?.Low,
            latestCandle?.Volume,
            previousClose);
    }

    private static YahooCandle? TryGetLatestYahooCandle(JsonElement item)
    {
        if (!item.TryGetProperty("timestamp", out var timestamps) ||
            timestamps.ValueKind != JsonValueKind.Array ||
            !item.TryGetProperty("indicators", out var indicators) ||
            !indicators.TryGetProperty("quote", out var quotes) ||
            quotes.ValueKind != JsonValueKind.Array ||
            quotes.GetArrayLength() == 0)
        {
            return null;
        }

        var quote = quotes[0];
        if (!quote.TryGetProperty("close", out var closes) ||
            closes.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        quote.TryGetProperty("open", out var opens);
        quote.TryGetProperty("high", out var highs);
        quote.TryGetProperty("low", out var lows);
        quote.TryGetProperty("volume", out var volumes);

        var count = Math.Min(timestamps.GetArrayLength(), closes.GetArrayLength());
        for (var index = count - 1; index >= 0; index--)
        {
            var close = GetOptionalDecimal(closes[index]);
            var timestamp = GetOptionalUnixTime(timestamps[index]);
            if (close is null || timestamp is null)
            {
                continue;
            }

            return new YahooCandle(
                timestamp.Value,
                close.Value,
                GetOptionalDecimal(opens, index),
                GetOptionalDecimal(highs, index),
                GetOptionalDecimal(lows, index),
                GetOptionalDecimal(volumes, index));
        }

        return null;
    }

    private static StooqQuote ParseStooqQuote(string content, string symbol)
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

    private static decimal? GetOptionalDecimal(JsonElement parent, string propertyName)
    {
        return parent.TryGetProperty(propertyName, out var element)
            ? GetOptionalDecimal(element)
            : null;
    }

    private static decimal? GetOptionalDecimal(JsonElement array, int index)
    {
        return array.ValueKind == JsonValueKind.Array && index < array.GetArrayLength()
            ? GetOptionalDecimal(array[index])
            : null;
    }

    private static decimal? GetOptionalDecimal(JsonElement element)
    {
        if (element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out var value))
        {
            return value;
        }

        return null;
    }

    private static DateTime? GetOptionalUnixTime(JsonElement parent, string propertyName)
    {
        return parent.TryGetProperty(propertyName, out var element)
            ? GetOptionalUnixTime(element)
            : null;
    }

    private static DateTime? GetOptionalUnixTime(JsonElement element)
    {
        return element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out var value) && value > 0
            ? DateTimeOffset.FromUnixTimeSeconds(value).UtcDateTime
            : null;
    }

    private static MarketDataResult MapStooqToMarketDataResult(
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
            StooqSource,
            quote.Volume,
            quote.Open,
            quote.High,
            quote.Low);
    }

    private sealed record YahooQuote(
        decimal Price,
        string? Currency,
        DateTime CapturedAtUtc,
        decimal? Open,
        decimal? High,
        decimal? Low,
        decimal? Volume,
        decimal? PreviousClose);

    private sealed record YahooCandle(
        DateTime CapturedAtUtc,
        decimal Close,
        decimal? Open,
        decimal? High,
        decimal? Low,
        decimal? Volume);

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
