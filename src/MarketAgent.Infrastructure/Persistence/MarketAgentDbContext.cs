using Microsoft.EntityFrameworkCore;

namespace MarketAgent.Infrastructure.Persistence;

public sealed class MarketAgentDbContext : DbContext
{
    public MarketAgentDbContext(DbContextOptions<MarketAgentDbContext> options)
        : base(options)
    {
    }

    public DbSet<PersistedSignalSnapshot> SignalSnapshots => Set<PersistedSignalSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

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
    }
}
