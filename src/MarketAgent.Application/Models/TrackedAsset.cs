using MarketAgent.Domain.Enums;

namespace MarketAgent.Application.Models;

public sealed record TrackedAsset(
    string Symbol,
    AssetType AssetType,
    string Currency);
