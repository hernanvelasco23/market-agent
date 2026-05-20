using Microsoft.EntityFrameworkCore;

namespace MarketAgent.Infrastructure.Persistence;

public sealed class MarketAgentDbContext : DbContext
{
    public MarketAgentDbContext(DbContextOptions<MarketAgentDbContext> options)
        : base(options)
    {
    }

    public DbSet<PersistedSignalSnapshot> SignalSnapshots => Set<PersistedSignalSnapshot>();

    public DbSet<PersistedSignalOutcome> SignalOutcomes => Set<PersistedSignalOutcome>();

    public DbSet<PersistedMarketSnapshot> MarketSnapshots => Set<PersistedMarketSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        var marketSnapshot = modelBuilder.Entity<PersistedMarketSnapshot>();

        marketSnapshot.ToTable("MarketSnapshots");
        marketSnapshot.HasKey(snapshot => snapshot.Id);
        marketSnapshot.Property(snapshot => snapshot.Symbol).HasMaxLength(32).IsRequired();
        marketSnapshot.Property(snapshot => snapshot.AssetType).IsRequired();
        marketSnapshot.Property(snapshot => snapshot.Price).HasPrecision(18, 6).IsRequired();
        marketSnapshot.Property(snapshot => snapshot.Currency).HasMaxLength(8).IsRequired();
        marketSnapshot.Property(snapshot => snapshot.CapturedAtUtc).IsRequired();
        marketSnapshot.Property(snapshot => snapshot.Source).HasMaxLength(128).IsRequired();
        marketSnapshot.Property(snapshot => snapshot.Volume).HasPrecision(18, 2);
        marketSnapshot.Property(snapshot => snapshot.OpenPrice).HasPrecision(18, 6);
        marketSnapshot.Property(snapshot => snapshot.HighPrice).HasPrecision(18, 6);
        marketSnapshot.Property(snapshot => snapshot.LowPrice).HasPrecision(18, 6);
        marketSnapshot.Property(snapshot => snapshot.PreviousClose).HasPrecision(18, 6);

        marketSnapshot.HasIndex(snapshot => snapshot.Symbol)
            .HasDatabaseName("IX_MarketSnapshots_Symbol");
        marketSnapshot.HasIndex(snapshot => snapshot.CapturedAtUtc)
            .HasDatabaseName("IX_MarketSnapshots_CapturedAtUtc");
        marketSnapshot.HasIndex(snapshot => new { snapshot.Symbol, snapshot.CapturedAtUtc })
            .HasDatabaseName("IX_MarketSnapshots_Symbol_CapturedAtUtc");
        marketSnapshot.HasIndex(snapshot => new { snapshot.Symbol, snapshot.Source, snapshot.CapturedAtUtc })
            .IsUnique()
            .HasDatabaseName("IX_MarketSnapshots_Symbol_Source_CapturedAtUtc");

        var signalSnapshot = modelBuilder.Entity<PersistedSignalSnapshot>();

        signalSnapshot.ToTable("SignalSnapshots");
        signalSnapshot.HasKey(snapshot => snapshot.Id);
        signalSnapshot.Property(snapshot => snapshot.CreatedAtUtc).IsRequired();
        signalSnapshot.Property(snapshot => snapshot.RunId).IsRequired();
        signalSnapshot.Property(snapshot => snapshot.Symbol).HasMaxLength(32).IsRequired();
        signalSnapshot.Property(snapshot => snapshot.Setup).HasMaxLength(128).IsRequired();
        signalSnapshot.Property(snapshot => snapshot.Action).HasMaxLength(128).IsRequired();
        signalSnapshot.Property(snapshot => snapshot.Score).HasPrecision(9, 2).IsRequired();
        signalSnapshot.Property(snapshot => snapshot.Confidence).HasMaxLength(64).IsRequired();
        signalSnapshot.Property(snapshot => snapshot.Price).HasPrecision(18, 6);
        signalSnapshot.Property(snapshot => snapshot.RelativeStrengthVsSpy).HasPrecision(9, 2);
        signalSnapshot.Property(snapshot => snapshot.RelativeVolume).HasPrecision(9, 2);
        signalSnapshot.Property(snapshot => snapshot.ExtensionFromEma20Percent).HasPrecision(9, 2);
        signalSnapshot.Property(snapshot => snapshot.MarketRegime).HasMaxLength(64);
        signalSnapshot.Property(snapshot => snapshot.TriggeredAlertsJson).HasColumnType("nvarchar(max)");
        signalSnapshot.Property(snapshot => snapshot.Ema9).HasPrecision(18, 6);
        signalSnapshot.Property(snapshot => snapshot.Ema20).HasPrecision(18, 6);
        signalSnapshot.Property(snapshot => snapshot.Ema50).HasPrecision(18, 6);
        signalSnapshot.Property(snapshot => snapshot.Rsi).HasPrecision(9, 2);
        signalSnapshot.Property(snapshot => snapshot.Source).HasMaxLength(64).IsRequired();
        signalSnapshot.Property(snapshot => snapshot.Timeframe).HasMaxLength(64);
        signalSnapshot.Property(snapshot => snapshot.Reason).HasColumnType("nvarchar(max)");
        signalSnapshot.Property(snapshot => snapshot.SignalType).HasMaxLength(64);
        signalSnapshot.Property(snapshot => snapshot.Entry).HasPrecision(18, 6);
        signalSnapshot.Property(snapshot => snapshot.Stop).HasPrecision(18, 6);
        signalSnapshot.Property(snapshot => snapshot.Target).HasPrecision(18, 6);
        signalSnapshot.Property(snapshot => snapshot.ScoreBreakdownJson).HasColumnType("nvarchar(max)");
        signalSnapshot.Property(snapshot => snapshot.OpenGapPercent).HasPrecision(9, 2);
        signalSnapshot.Property(snapshot => snapshot.RecoveryFromLowPercent).HasPrecision(9, 2);

        signalSnapshot.HasIndex(snapshot => snapshot.CreatedAtUtc)
            .HasDatabaseName("IX_SignalSnapshots_CreatedAtUtc");
        signalSnapshot.HasIndex(snapshot => snapshot.Symbol)
            .HasDatabaseName("IX_SignalSnapshots_Symbol");
        signalSnapshot.HasIndex(snapshot => snapshot.RunId)
            .HasDatabaseName("IX_SignalSnapshots_RunId");
        signalSnapshot.HasIndex(snapshot => new { snapshot.Symbol, snapshot.CreatedAtUtc })
            .HasDatabaseName("IX_SignalSnapshots_Symbol_CreatedAtUtc");

        var signalOutcome = modelBuilder.Entity<PersistedSignalOutcome>();

        signalOutcome.ToTable("SignalOutcomes");
        signalOutcome.HasKey(outcome => outcome.Id);
        signalOutcome.Property(outcome => outcome.SignalSnapshotId).IsRequired();
        signalOutcome.Property(outcome => outcome.EvaluatedAtUtc).IsRequired();
        signalOutcome.Property(outcome => outcome.EvaluationStatus).HasMaxLength(32).IsRequired();
        signalOutcome.Property(outcome => outcome.PriceAtSignal).HasPrecision(18, 6);
        signalOutcome.Property(outcome => outcome.PriceAfter15Minutes).HasPrecision(18, 6);
        signalOutcome.Property(outcome => outcome.PriceAfter1Hour).HasPrecision(18, 6);
        signalOutcome.Property(outcome => outcome.PriceAfter4Hours).HasPrecision(18, 6);
        signalOutcome.Property(outcome => outcome.PriceAfter1Day).HasPrecision(18, 6);
        signalOutcome.Property(outcome => outcome.MaxRunupPercent).HasPrecision(9, 2);
        signalOutcome.Property(outcome => outcome.MaxDrawdownPercent).HasPrecision(9, 2);
        signalOutcome.Property(outcome => outcome.OutcomePercent).HasPrecision(9, 2);
        signalOutcome.Property(outcome => outcome.FailureReason).HasMaxLength(256);

        signalOutcome.HasOne(outcome => outcome.SignalSnapshot)
            .WithOne(snapshot => snapshot.Outcome)
            .HasForeignKey<PersistedSignalOutcome>(outcome => outcome.SignalSnapshotId)
            .OnDelete(DeleteBehavior.Cascade);

        signalOutcome.HasIndex(outcome => outcome.SignalSnapshotId)
            .IsUnique()
            .HasDatabaseName("IX_SignalOutcomes_SignalSnapshotId");
        signalOutcome.HasIndex(outcome => outcome.EvaluatedAtUtc)
            .HasDatabaseName("IX_SignalOutcomes_EvaluatedAtUtc");
        signalOutcome.HasIndex(outcome => outcome.EvaluationStatus)
            .HasDatabaseName("IX_SignalOutcomes_EvaluationStatus");
        signalOutcome.HasIndex(outcome => outcome.IsSuccessful)
            .HasDatabaseName("IX_SignalOutcomes_IsSuccessful");
    }
}
