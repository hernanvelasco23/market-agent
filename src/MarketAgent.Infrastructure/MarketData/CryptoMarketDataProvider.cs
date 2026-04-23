using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Models;
using MarketAgent.Domain.Enums;

namespace MarketAgent.Infrastructure.MarketData;

public sealed class CryptoMarketDataProvider : IMarketDataProvider
{
    private const string Source = "Coinbase";
    private static readonly Uri BaseAddress = new("https://api.coinbase.com/");

    private static readonly IReadOnlyDictionary<string, string> ProviderSymbols =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["BTC"] = "BTC-USD",
            ["ETH"] = "ETH-USD"
        };

    private readonly HttpClient _httpClient;

    public CryptoMarketDataProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress ??= BaseAddress;
    }

    public async Task<MarketDataResult> GetLatestAsync(
        TrackedAsset asset,
        CancellationToken cancellationToken = default)
    {
        ValidateAssetType(asset.AssetType);

        var symbol = NormalizeSymbol(asset.Symbol);
        var providerSymbol = MapToProviderSymbol(symbol);
        var requestUri = BuildSpotPriceRequestUri(providerSymbol);

        using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var spotPrice = await ParseSpotPriceAsync(contentStream, symbol, cancellationToken);

        return new MarketDataResult(
            symbol,
            asset.AssetType,
            spotPrice.Price,
            spotPrice.Currency,
            DateTime.UtcNow,
            Source);
    }

    private static void ValidateAssetType(AssetType assetType)
    {
        if (assetType == AssetType.Crypto)
        {
            return;
        }

        throw new NotSupportedException(
            $"{nameof(CryptoMarketDataProvider)} supports only crypto assets.");
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
            $"Symbol '{symbol}' is not supported by {nameof(CryptoMarketDataProvider)}.");
    }

    private static string BuildSpotPriceRequestUri(string providerSymbol)
    {
        return $"v2/prices/{providerSymbol}/spot";
    }

    private static async Task<CoinbaseSpotPrice> ParseSpotPriceAsync(
        Stream contentStream,
        string symbol,
        CancellationToken cancellationToken)
    {
        var response = await JsonSerializer.DeserializeAsync<CoinbaseSpotPriceResponse>(
            contentStream,
            cancellationToken: cancellationToken);

        if (response?.Data is null)
        {
            throw new InvalidOperationException(
                $"Market data response for '{symbol}' did not contain spot price data.");
        }

        if (string.IsNullOrWhiteSpace(response.Data.Currency))
        {
            throw new InvalidOperationException(
                $"Market data response for '{symbol}' did not contain currency data.");
        }

        if (!decimal.TryParse(
                response.Data.Amount,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var price))
        {
            throw new InvalidOperationException(
                $"Market data response for '{symbol}' had an invalid price value.");
        }

        return new CoinbaseSpotPrice(
            price,
            response.Data.Currency.Trim().ToUpperInvariant());
    }

    private sealed record CoinbaseSpotPrice(
        decimal Price,
        string Currency);

    private sealed class CoinbaseSpotPriceResponse
    {
        [JsonPropertyName("data")]
        public CoinbaseSpotPriceData? Data { get; set; }
    }

    private sealed class CoinbaseSpotPriceData
    {
        [JsonPropertyName("amount")]
        public string? Amount { get; set; }

        [JsonPropertyName("currency")]
        public string? Currency { get; set; }
    }
}
