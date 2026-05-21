using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Models;
using MarketAgent.Domain.Enums;

namespace MarketAgent.Infrastructure.Watchlists;

public sealed class StaticWatchlistProvider : IWatchlistProvider
{
    private static readonly IReadOnlyCollection<TrackedAsset> Assets =
    [
        new("MU", AssetType.Equity, "USD"),
        new("AMZN", AssetType.Equity, "USD"),
        new("V", AssetType.Equity, "USD"),
        new("ASTS", AssetType.Equity, "USD"),
        new("NKE", AssetType.Equity, "USD"),
        new("PLTR", AssetType.Equity, "USD"),
        new("PATH", AssetType.Equity, "USD"),
        new("IBM", AssetType.Equity, "USD"),
        new("META", AssetType.Equity, "USD"),
        new("GOOG", AssetType.Equity, "USD"),
        new("RKLB", AssetType.Equity, "USD"),
        new("RGTI", AssetType.Equity, "USD"),
        new("SE", AssetType.Equity, "USD"),
        new("MELI", AssetType.Equity, "USD"),
        new("TSLA", AssetType.Equity, "USD"),
        new("NU", AssetType.Equity, "USD"),
        new("NVDA", AssetType.Equity, "USD"),
        new("MSFT", AssetType.Equity, "USD"),
        new("AMD", AssetType.Equity, "USD"),
        new("SPY", AssetType.Etf, "USD"),
        new("BTC", AssetType.Crypto, "USD"),
        new("ETH", AssetType.Crypto, "USD"),
        new("MEP", AssetType.ExchangeRate, "ARS")
    ];

    public Task<IReadOnlyCollection<TrackedAsset>> GetTrackedAssetsAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Assets);
    }
}
