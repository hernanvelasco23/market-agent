namespace MarketAgent.Application.Models;

public sealed class MarketAgentSchedulerOptions
{
    public const string SectionName = "MarketAgentScheduler";
    public const int DefaultIntervalMinutes = 5;

    public bool Enabled { get; set; } = false;
    public int IntervalMinutes { get; set; } = DefaultIntervalMinutes;
    public bool RunEmailDelivery { get; set; } = false;
    public bool MarketHoursOnly { get; set; } = true;
    public bool RunOnStartup { get; set; } = false;

    public int GetSafeIntervalMinutes()
    {
        return IntervalMinutes <= 0
            ? DefaultIntervalMinutes
            : IntervalMinutes;
    }
}
