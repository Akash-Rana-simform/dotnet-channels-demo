using System.Threading.Channels;
using OrderNotificationDemo.Models;

namespace OrderNotificationDemo.Services;

public class EmailWorker(
    Channel<EmailMessage> channel,
    IEmailService emailService,
    ILogger<EmailWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("[EmailWorker] Started — waiting for emails on the channel...");
        Console.ResetColor();

        // One failed email must not kill the worker — without this try/catch, an
        // exception here would end ExecuteAsync and the channel would stop being drained.
        await foreach (var message in channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await emailService.SendAsync(message, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Email failed for {To}", message.To);
            }
        }
    }
}
