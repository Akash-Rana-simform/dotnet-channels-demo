using System.Threading.Channels;
using OrderNotificationDemo.Models;

namespace OrderNotificationDemo.Services;

public class EmailWorker : BackgroundService
{
    private readonly Channel<EmailMessage> _channel;
    private readonly IEmailService _emailService;

    public EmailWorker(Channel<EmailMessage> channel, IEmailService emailService)
    {
        _channel = channel;
        _emailService = emailService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("[EmailWorker] Started — waiting for emails on the channel...");
        Console.ResetColor();

        await foreach (var message in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            await _emailService.SendAsync(message, stoppingToken);
        }
    }
}
