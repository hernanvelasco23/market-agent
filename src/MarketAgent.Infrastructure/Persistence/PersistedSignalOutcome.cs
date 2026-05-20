namespace MarketAgent.Infrastructure.Persistence;

public sealed class PersistedSignalOutcome
{
    public Guid Id { get; set; }

    public Guid SignalSnapshotId { get; set; }

    public DateTime EvaluatedAtUtc { get; set; }

    public string EvaluationStatus { get; set; } = string.Empty;

    public decimal? PriceAtSignal { get; set; }

    public decimal? PriceAfter15Minutes { get; set; }

    public decimal? PriceAfter1Hour { get; set; }

    public decimal? PriceAfter4Hours { get; set; }

    public decimal? PriceAfter1Day { get; set; }

    public decimal? MaxRunupPercent { get; set; }

    public decimal? MaxDrawdownPercent { get; set; }

    public decimal? OutcomePercent { get; set; }

    public bool? IsSuccessful { get; set; }

    public string? FailureReason { get; set; }

    public PersistedSignalSnapshot SignalSnapshot { get; set; } = null!;
}
