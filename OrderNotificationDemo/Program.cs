// ============================================================================
// OrderNotificationDemo — System.Threading.Channels live demo
// ============================================================================
//
// Run:
//   dotnet run
//
// Swagger UI:
//   http://localhost:5000/swagger
//
// Single order (returns immediately — email is sent in the background):
//   curl -X POST http://localhost:5000/api/orders -H "Content-Type: application/json" -d '{"name":"Akash","email":"akash@test.com"}'
//
// Burst test (watch the queue fill up and backpressure kick in):
//   curl -X POST "http://localhost:5000/api/orders/burst?count=10"
//
// Check the channel queue depth:
//   curl http://localhost:5000/api/orders/channel-status
//
// Comparison — same request, but WITHOUT a channel (blocks for ~2s):
//   curl -X POST http://localhost:5000/api/orders/slow -H "Content-Type: application/json" -d '{"name":"Akash","email":"akash@test.com"}'
// ============================================================================

using System.Text;
using System.Threading.Channels;
using OrderNotificationDemo.Models;
using OrderNotificationDemo.Services;

Console.OutputEncoding = Encoding.UTF8;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Bounded channel with capacity 5 — small on purpose, so backpressure is
// visible with just a handful of requests (see /api/orders/burst).
builder.Services.AddSingleton(Channel.CreateBounded<EmailMessage>(
    new BoundedChannelOptions(5)
    {
        FullMode = BoundedChannelFullMode.Wait
    }));

builder.Services.AddSingleton<IEmailService, ConsoleEmailService>();
builder.Services.AddHostedService<EmailWorker>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("============================================================");
Console.WriteLine(" OrderNotificationDemo — System.Threading.Channels Demo");
Console.WriteLine(" Swagger UI: http://localhost:5000/swagger");
Console.WriteLine("============================================================");
Console.ResetColor();

app.Run();
