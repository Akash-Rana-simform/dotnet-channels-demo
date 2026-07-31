using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.AspNetCore.Mvc;
using OrderNotificationDemo.Models;
using OrderNotificationDemo.Services;

namespace OrderNotificationDemo.Controllers;

[ApiController]
[Route("api/orders")]
public class OrderController(
    Channel<EmailMessage> emailChannel,
    IEmailService emailService,
    ChannelCapacityOptions channelCapacity) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> PlaceOrder([FromBody] OrderDto dto, CancellationToken cancellationToken)
    {
        var orderId = Guid.NewGuid().ToString("N")[..8];
        // Drop email into the channel and move on
        await emailChannel.Writer.WriteAsync(new EmailMessage(
            dto.Email,
            $"Order {orderId} confirmed",
            $"Hi {dto.Name}, your order is on its way!"), cancellationToken);

        LogOrderPlaced(orderId);

        return Ok(new { OrderId = orderId, Status = "Confirmed" });
    }

    [HttpPost("burst")]
    public async Task<IActionResult> Burst([FromQuery] int count = 10, CancellationToken cancellationToken = default)
    {
        var orderIds = new List<string>();

        for (var i = 0; i < count; i++)
        {
            var orderId = Guid.NewGuid().ToString("N")[..8];
            var email = new EmailMessage($"user{i + 1}@test.com", $"Order {orderId} confirmed", $"Order {orderId} confirmed!");

            await emailChannel.Writer.WriteAsync(email, cancellationToken);
            orderIds.Add(orderId);

            LogOrderPlaced(orderId);
        }

        return Ok(new { Count = count, OrderIds = orderIds, Status = "Confirmed" });
    }

    [HttpPost("slow")]
    public async Task<IActionResult> PlaceOrderSlow([FromBody] OrderDto dto, CancellationToken cancellationToken)
    {
        var orderId = Guid.NewGuid().ToString("N")[..8];
        var email = new EmailMessage(dto.Email, $"Order {orderId} confirmed", $"Hi {dto.Name}, your order is on its way!");

        var stopwatch = Stopwatch.StartNew();
        await emailService.SendAsync(email, cancellationToken);
        stopwatch.Stop();

        return Ok(new { OrderId = orderId, Status = "Confirmed", ElapsedMs = stopwatch.ElapsedMilliseconds });
    }

    [HttpGet("channel-status")]
    public IActionResult ChannelStatus()
    {
        return Ok(new
        {
            QueueCount = emailChannel.Reader.Count,
            Capacity = channelCapacity.Capacity,
            IsCompleted = emailChannel.Reader.Completion.IsCompleted
        });
    }

    private void LogOrderPlaced(string orderId)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[{timestamp}] 🟢 Order {orderId} placed — email queued (Queue: {emailChannel.Reader.Count}/{channelCapacity.Capacity})");
        Console.ForegroundColor = originalColor;
    }
}
