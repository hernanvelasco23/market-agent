using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Models;

namespace MarketAgent.Infrastructure.MarketData;

public sealed class MarketDataProviderResolver : IMarketDataProviderResolver
{
    private readonly IReadOnlyCollection<IMarketDataProvider> _providers;

    public MarketDataProviderResolver(IEnumerable<IMarketDataProvider> providers)
    {
        _providers = providers.ToArray();
    }

    public IMarketDataProvider Resolve(TrackedAsset asset)
    {
        var provider = _providers.FirstOrDefault(candidate => candidate.CanHandle(asset));

        if (provider is not null)
        {
            return provider;
        }

        throw new NotSupportedException(
            $"No market data provider is registered for asset '{asset.Symbol}' ({asset.AssetType}).");
    }
}
