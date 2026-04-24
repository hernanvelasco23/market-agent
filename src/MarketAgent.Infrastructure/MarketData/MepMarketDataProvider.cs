using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Models;
using MarketAgent.Domain.Enums;

namespace MarketAgent.Infrastructure.MarketData;

public sealed class MepMarketDataProvider : IMarketDataProvider
{
    private const string Source = "DolarApi";
    private const string SupportedSymbol = "MEP";
    private static readonly Uri BaseAddress = new("https://dolarapi.com/");

    private readonly HttpClient _httpClient;

    public MepMarketDataProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress ??= BaseAddress;
    }

    public bool CanHandle(TrackedAsset asset)
    {
        return asset.AssetType == AssetType.ExchangeRate
            && NormalizeSymbol(asset.Symbol) == SupportedSymbol;
    }

    public async Task<MarketDataResult> GetLatestAsync(
        TrackedAsset asset,
        CancellationToken cancellationToken = default)
    {
        ValidateAssetType(asset.AssetType);

        var symbol = NormalizeSymbol(asset.Symbol);
        ValidateSymbol(symbol);

        using var response = await _httpClient.GetAsync("v1/dolares/bolsa", cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var quote = await ParseQuoteAsync(contentStream, symbol, cancellationToken);

        return new MarketDataResult(
            SupportedSymbol,
            AssetType.ExchangeRate,
            quote.Price,
            quote.Currency,
            quote.CapturedAtUtc,
            Source);
    }

    private static void ValidateAssetType(AssetType assetType)
    {
        if (assetType == AssetType.ExchangeRate)
        {
            return;
        }

        throw new NotSupportedException(
            $"{nameof(MepMarketDataProvider)} supports only exchange-rate assets.");
    }

    private static string NormalizeSymbol(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Asset symbol is required.", nameof(symbol));
        }

        return symbol.Trim().ToUpperInvariant();
    }

    private static void ValidateSymbol(string symbol)
    {
        if (symbol == SupportedSymbol)
        {
            return;
        }

        throw new NotSupportedException(
            $"Symbol '{symbol}' is not supported by {nameof(MepMarketDataProvider)}.");
    }

    private static async Task<MepQuote> ParseQuoteAsync(
        Stream contentStream,
        string symbol,
        CancellationToken cancellationToken)
    {
        var response = await JsonSerializer.DeserializeAsync<MepResponse>(
            contentStream,
            cancellationToken: cancellationToken);

        if (response is null)
        {
            throw new InvalidOperationException(
                $"Market data response for '{symbol}' did not contain quote data.");
        }

        if (!response.Venta.HasValue)
        {
            throw new InvalidOperationException(
                $"Market data response for '{symbol}' did not contain a valid sale price.");
        }

        if (string.IsNullOrWhiteSpace(response.Moneda))
        {
            throw new InvalidOperationException(
                $"Market data response for '{symbol}' did not contain currency data.");
        }

        if (!DateTime.TryParse(
                response.FechaActualizacion,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var capturedAtUtc))
        {
            throw new InvalidOperationException(
                $"Market data response for '{symbol}' had an invalid timestamp.");
        }

        return new MepQuote(
            response.Venta.Value,
            response.Moneda.Trim().ToUpperInvariant(),
            DateTime.SpecifyKind(capturedAtUtc, DateTimeKind.Utc));
    }

    private sealed record MepQuote(
        decimal Price,
        string Currency,
        DateTime CapturedAtUtc);

    private sealed class MepResponse
    {
        [JsonPropertyName("venta")]
        public decimal? Venta { get; set; }

        [JsonPropertyName("moneda")]
        public string? Moneda { get; set; }

        [JsonPropertyName("fechaActualizacion")]
        public string? FechaActualizacion { get; set; }
    }
}
