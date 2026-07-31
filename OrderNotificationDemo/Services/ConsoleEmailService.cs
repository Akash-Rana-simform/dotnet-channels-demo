using System.Diagnostics;
using OrderNotificationDemo.Models;

namespace OrderNotificationDemo.Services;

public class ConsoleEmailService : IEmailService
{
    private static readonly TimeSpan SimulatedSendDelay = TimeSpan.FromSeconds(2);

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        Log(ConsoleColor.Yellow, $"📧 Sending email to {message.To}...");

        var stopwatch = Stopwatch.StartNew();
        await Task.Delay(SimulatedSendDelay, cancellationToken);
        stopwatch.Stop();

        Log(ConsoleColor.Green, $"✅ Email sent to {message.To} (took {stopwatch.ElapsedMilliseconds}ms)");
    }

    private static void Log(ConsoleColor color, string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine($"[{timestamp}] {message}");
        Console.ForegroundColor = originalColor;
    }
}
