namespace MarketAgent.Infrastructure.Persistence;

public sealed class PersistedAlertEvent
{
    public Guid Id { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public Guid SignalSnapshotId { get; set; }

    public Guid RunId { get; set; }

    public string Symbol { get; set; } = string.Empty;

    public string Setup { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public decimal Score { get; set; }

    public string Confidence { get; set; } = string.Empty;

    public decimal PriceAtSignal { get; set; }

    public string AlertType { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string ReasonJson { get; set; } = string.Empty;

    public string DeliveryStatus { get; set; } = string.Empty;

    public PersistedSignalSnapshot? SignalSnapshot { get; set; }
}
