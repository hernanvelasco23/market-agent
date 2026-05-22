using MarketAgent.Application.Models;

namespace MarketAgent.Application.Alerts;

public sealed record AlertUpside(
    decimal Entry,
    decimal TakeProfit,
    decimal? RiskReward,
    decimal PotentialUpsidePercent);

public static class AlertUpsideCalculator
{
    public static AlertUpside? Calculate(AlertEventItem alert)
    {
        if (alert.Entry is not > 0m)
        {
            return null;
        }

        var target = SelectTarget(alert, alert.Entry.Value);
        if (target is null)
        {
            return null;
        }

        var potentialUpside = Math.Round(
            ((target.Value.TakeProfit - alert.Entry.Value) / alert.Entry.Value) * 100m,
            2,
            MidpointRounding.AwayFromZero);

        return new AlertUpside(
            alert.Entry.Value,
            target.Value.TakeProfit,
            target.Value.RiskReward,
            potentialUpside);
    }

    private static (decimal TakeProfit, decimal? RiskReward)? SelectTarget(
        AlertEventItem alert,
        decimal entry)
    {
        var candidates = new[]
        {
            (TakeProfit: alert.TakeProfit2, RiskReward: alert.RiskReward2),
            (TakeProfit: alert.TakeProfit1, RiskReward: alert.RiskReward1),
            (TakeProfit: alert.TakeProfit3, RiskReward: alert.RiskReward3)
        };

        foreach (var candidate in candidates)
        {
            if (candidate.TakeProfit is > 0m && candidate.TakeProfit.Value > entry)
            {
                return (candidate.TakeProfit.Value, candidate.RiskReward);
            }
        }

        return null;
    }
}
