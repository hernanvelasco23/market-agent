using MarketAgent.Application.Models;

namespace MarketAgent.Application.Abstractions;

public interface IMarketDataProviderResolver
{
    IMarketDataProvider Resolve(TrackedAsset asset);
}
