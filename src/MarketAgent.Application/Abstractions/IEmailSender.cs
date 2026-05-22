using MarketAgent.Application.Models;

namespace MarketAgent.Application.Abstractions;

public interface IEmailSender
{
    Task SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default);
}
