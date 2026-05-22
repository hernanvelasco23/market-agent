namespace MarketAgent.Application.Models;

public sealed class EmailDeliveryOptions
{
    public const string SectionName = "EmailDelivery";

    public string? SmtpHost { get; set; }

    public int SmtpPort { get; set; } = 587;

    public string? SmtpUser { get; set; }

    public string? SmtpPassword { get; set; }

    public string? FromEmail { get; set; }

    public string FromName { get; set; } = "MarketAgent";

    public string? ToEmail { get; set; }

    public bool EnableSsl { get; set; } = true;
}
