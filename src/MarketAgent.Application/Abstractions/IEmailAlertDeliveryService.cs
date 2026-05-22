using MarketAgent.Application.Models;

namespace MarketAgent.Application.Abstractions;

public interface IEmailAlertDeliveryService
{
    Task<EmailAlertDeliveryResult> DeliverAsync(
        EmailAlertDeliveryRequest request,
        CancellationToken cancellationToken = default);
}
