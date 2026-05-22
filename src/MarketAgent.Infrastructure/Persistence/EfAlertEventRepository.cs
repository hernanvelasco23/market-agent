using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace MarketAgent.Infrastructure.Persistence;

public sealed class EfAlertEventRepository : IAlertEventRepository
{
    private readonly MarketAgentDbContext _dbContext;

    public EfAlertEventRepository(MarketAgentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<AlertEventItem>> GetRecentAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.AlertEvents
            .AsNoTracking()
            .OrderByDescending(alertEvent => alertEvent.CreatedAtUtc)
            .Take(limit)
            .Select(alertEvent => new AlertEventItem(
                alertEvent.Id,
                alertEvent.CreatedAtUtc,
                alertEvent.SignalSnapshotId,
                alertEvent.RunId,
                alertEvent.Symbol,
                alertEvent.Setup,
                alertEvent.Action,
                alertEvent.Score,
                alertEvent.Confidence,
                alertEvent.PriceAtSignal,
                alertEvent.AlertType,
                alertEvent.Title,
                alertEvent.Message,
                alertEvent.ReasonJson,
                alertEvent.DeliveryStatus,
                null,
                null,
                null,
                null,
                null,
                null,
                null))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<AlertEventItem>> GetPendingDeliveryAsync(
        int limit,
        bool includeFailed,
        DateTime? sinceUtc,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.AlertEvents
            .AsNoTracking()
            .Where(alertEvent =>
                alertEvent.DeliveryStatus == AlertDeliveryStatuses.InternalOnly ||
                (includeFailed && alertEvent.DeliveryStatus == AlertDeliveryStatuses.DeliveryFailed));

        if (sinceUtc is not null)
        {
            query = query.Where(alertEvent => alertEvent.CreatedAtUtc >= sinceUtc.Value);
        }

        return await query
            .OrderByDescending(alertEvent => alertEvent.Score)
            .ThenByDescending(alertEvent =>
                alertEvent.Confidence == "High" ? 3 :
                alertEvent.Confidence == "Medium" ? 2 :
                alertEvent.Confidence == "Low" ? 1 : 0)
            .ThenByDescending(alertEvent => alertEvent.CreatedAtUtc)
            .Take(limit)
            .Select(alertEvent => new AlertEventItem(
                alertEvent.Id,
                alertEvent.CreatedAtUtc,
                alertEvent.SignalSnapshotId,
                alertEvent.RunId,
                alertEvent.Symbol,
                alertEvent.Setup,
                alertEvent.Action,
                alertEvent.Score,
                alertEvent.Confidence,
                alertEvent.PriceAtSignal,
                alertEvent.AlertType,
                alertEvent.Title,
                alertEvent.Message,
                alertEvent.ReasonJson,
                alertEvent.DeliveryStatus,
                alertEvent.SignalSnapshot == null ? null : alertEvent.SignalSnapshot.Entry,
                null,
                alertEvent.SignalSnapshot == null ? null : alertEvent.SignalSnapshot.Target,
                null,
                null,
                null,
                null))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<int> CountStalePendingDeliveryAsync(
        bool includeFailed,
        DateTime sinceUtc,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.AlertEvents
            .AsNoTracking()
            .Where(alertEvent =>
                alertEvent.CreatedAtUtc < sinceUtc &&
                (alertEvent.DeliveryStatus == AlertDeliveryStatuses.InternalOnly ||
                    (includeFailed && alertEvent.DeliveryStatus == AlertDeliveryStatuses.DeliveryFailed)))
            .CountAsync(cancellationToken);
    }

    public async Task<IReadOnlySet<string>> GetExistingAlertKeysAsync(
        IReadOnlyCollection<AlertEvaluationCandidate> candidates,
        string alertType,
        CancellationToken cancellationToken = default)
    {
        if (candidates.Count == 0)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var signalSnapshotIds = candidates
            .Select(candidate => candidate.SignalSnapshotId)
            .ToArray();
        var rows = await _dbContext.AlertEvents
            .AsNoTracking()
            .Where(alertEvent => alertEvent.AlertType == alertType &&
                signalSnapshotIds.Contains(alertEvent.SignalSnapshotId))
            .Select(alertEvent => new { alertEvent.SignalSnapshotId, alertEvent.AlertType })
            .ToArrayAsync(cancellationToken);

        return rows
            .Select(row => CreateAlertKey(row.SignalSnapshotId, row.AlertType))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public async Task SaveAsync(
        AlertEventRecord alertEvent,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.AlertEvents.AddAsync(ToEntity(alertEvent), cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateDeliveryStatusAsync(
        IReadOnlyCollection<Guid> alertEventIds,
        string deliveryStatus,
        CancellationToken cancellationToken = default)
    {
        if (alertEventIds.Count == 0)
        {
            return;
        }

        await _dbContext.AlertEvents
            .Where(alertEvent => alertEventIds.Contains(alertEvent.Id))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(alertEvent => alertEvent.DeliveryStatus, deliveryStatus),
                cancellationToken);
    }

    private static PersistedAlertEvent ToEntity(AlertEventRecord alertEvent)
    {
        return new PersistedAlertEvent
        {
            Id = alertEvent.Id == Guid.Empty ? Guid.NewGuid() : alertEvent.Id,
            CreatedAtUtc = alertEvent.CreatedAtUtc,
            SignalSnapshotId = alertEvent.SignalSnapshotId,
            RunId = alertEvent.RunId,
            Symbol = alertEvent.Symbol,
            Setup = alertEvent.Setup,
            Action = alertEvent.Action,
            Score = alertEvent.Score,
            Confidence = alertEvent.Confidence,
            PriceAtSignal = alertEvent.PriceAtSignal,
            AlertType = alertEvent.AlertType,
            Title = alertEvent.Title,
            Message = alertEvent.Message,
            ReasonJson = alertEvent.ReasonJson,
            DeliveryStatus = alertEvent.DeliveryStatus
        };
    }

    private static string CreateAlertKey(Guid signalSnapshotId, string alertType)
    {
        return $"{signalSnapshotId:N}:{alertType}";
    }
}
