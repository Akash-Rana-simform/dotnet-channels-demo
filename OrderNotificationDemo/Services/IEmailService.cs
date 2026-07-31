using OrderNotificationDemo.Models;

namespace OrderNotificationDemo.Services;

public interface IEmailService
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
