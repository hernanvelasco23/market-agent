namespace MarketAgent.Infrastructure.Persistence;

public sealed class PersistedSignalSnapshot
{
    public Guid Id { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public Guid RunId { get; set; }

    public string Symbol { get; set; } = string.Empty;

    public string Setup { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public decimal Score { get; set; }

    public string Confidence { get; set; } = string.Empty;

    public decimal? Price { get; set; }

    public decimal? RelativeStrengthVsSpy { get; set; }

    public decimal? RelativeVolume { get; set; }

    public decimal? ExtensionFromEma20Percent { get; set; }

    public string? MarketRegime { get; set; }

    public string? TriggeredAlertsJson { get; set; }

    public decimal? Ema9 { get; set; }

    public decimal? Ema20 { get; set; }

    public decimal? Ema50 { get; set; }

    public decimal? Rsi { get; set; }

    public string Source { get; set; } = string.Empty;

    public string? Timeframe { get; set; }

    public string? Reason { get; set; }

    public string? SignalType { get; set; }

    public decimal? Entry { get; set; }

    public decimal? Stop { get; set; }

    public decimal? Target { get; set; }

    public string? ScoreBreakdownJson { get; set; }

    public bool OpeningRedReversalDetected { get; set; }

    public decimal? OpenGapPercent { get; set; }

    public decimal? RecoveryFromLowPercent { get; set; }

    public PersistedSignalOutcome? Outcome { get; set; }

    public ICollection<PersistedAlertEvent> AlertEvents { get; set; } = [];
}
