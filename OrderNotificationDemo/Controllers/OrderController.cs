using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.AspNetCore.Mvc;
using OrderNotificationDemo.Models;
using OrderNotificationDemo.Services;

namespace OrderNotificationDemo.Controllers;

[ApiController]
[Route("api/orders")]
public class OrderController : ControllerBase
{
    // Must match the BoundedChannelOptions capacity registered in Program.cs.
    private const int ChannelCapacity = 5;

    private readonly Channel<EmailMessage> _channel;
    private readonly IEmailService _emailService;

    public OrderController(Channel<EmailMessage> channel, IEmailService emailService)
    {
        _channel = channel;
        _emailService = emailService;
    }

    [HttpPost]
    public async Task<IActionResult> PlaceOrder([FromBody] OrderDto order, CancellationToken cancellationToken)
    {
        var orderId = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var email = new EmailMessage(order.Email, "Order Confirmation", $"Hi {order.Name}, your order {orderId} is confirmed!");

        await _channel.Writer.WriteAsync(email, cancellationToken);

        LogOrderPlaced(orderId);

        return Ok(new { OrderId = orderId, Status = "Queued" });
    }

    [HttpPost("burst")]
    public async Task<IActionResult> Burst([FromQuery] int count = 10, CancellationToken cancellationToken = default)
    {
        var orderIds = new List<string>();

        for (var i = 0; i < count; i++)
        {
            var orderId = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
            var email = new EmailMessage($"user{i + 1}@test.com", "Order Confirmation", $"Order {orderId} confirmed!");

            await _channel.Writer.WriteAsync(email, cancellationToken);
            orderIds.Add(orderId);

            LogOrderPlaced(orderId);
        }

        return Ok(new { Count = count, OrderIds = orderIds, Status = "Queued" });
    }

    [HttpPost("slow")]
    public async Task<IActionResult> PlaceOrderSlow([FromBody] OrderDto order, CancellationToken cancellationToken)
    {
        var orderId = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var email = new EmailMessage(order.Email, "Order Confirmation", $"Hi {order.Name}, your order {orderId} is confirmed!");

        var stopwatch = Stopwatch.StartNew();
        await _emailService.SendAsync(email, cancellationToken);
        stopwatch.Stop();

        return Ok(new { OrderId = orderId, Status = "Sent", ElapsedMs = stopwatch.ElapsedMilliseconds });
    }

    [HttpGet("channel-status")]
    public IActionResult ChannelStatus()
    {
        return Ok(new
        {
            QueueCount = _channel.Reader.Count,
            Capacity = ChannelCapacity,
            IsCompleted = _channel.Reader.Completion.IsCompleted
        });
    }

    private void LogOrderPlaced(string orderId)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[{timestamp}] 🟢 Order {orderId} placed — email queued (Queue: {_channel.Reader.Count}/{ChannelCapacity})");
        Console.ForegroundColor = originalColor;
    }
}
