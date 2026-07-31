# dotnet-channels-demo

A small, live-training-friendly .NET 10 Web API that demonstrates
[`System.Threading.Channels`](https://learn.microsoft.com/dotnet/api/system.threading.channels)
as a producer/consumer queue between an HTTP request and a background worker.

**Scenario:** placing an order should respond instantly. Sending the confirmation
email is slow (2 seconds), so instead of making the caller wait, the controller
drops an `EmailMessage` onto a channel and returns immediately. A background
service (`EmailWorker`) drains the channel and does the slow work out-of-band.

## Why this is interesting to watch

- The channel is a **bounded** queue — once it's full, producers
  (`await channel.Writer.WriteAsync(...)`) start **waiting** instead of piling
  up unbounded work in memory. That's backpressure, visible live in the console.
  Capacity is configurable (see below) so you can demo it roomy first, then small.
- A `/slow` endpoint reproduces the same order flow *without* a channel — the
  request just awaits the 2-second send directly — so you can show the
  before/after side by side.

## Project structure

```
OrderNotificationDemo/
├── Controllers/
│   └── OrderController.cs      # POST /api/orders, /burst, /slow, GET /channel-status
├── Models/
│   ├── EmailMessage.cs         # record(To, Subject, Body) — what goes on the channel
│   └── OrderDto.cs             # record(Name, Email) — incoming request body
├── Services/
│   ├── IEmailService.cs
│   ├── ConsoleEmailService.cs  # simulates a slow email send (2s Task.Delay), logs to console
│   └── EmailWorker.cs          # BackgroundService — await foreach over channel.Reader.ReadAllAsync()
└── Program.cs                  # DI wiring, channel registration, Swagger
```

The channel itself is registered once in `Program.cs` as a singleton, with its
capacity read from `appsettings.json` (`ChannelSettings:Capacity`, default `500`):

```csharp
var channelCapacity = builder.Configuration.GetValue("ChannelSettings:Capacity", 500);
builder.Services.AddSingleton(Channel.CreateBounded<EmailMessage>(
    new BoundedChannelOptions(channelCapacity) { FullMode = BoundedChannelFullMode.Wait }));
```

`OrderController` gets the `Channel<EmailMessage>` injected and only ever
**writes** to it. `EmailWorker` gets the same singleton injected and only ever
**reads** from it — that's the whole demo.

### Changing the capacity for a live demo

Edit `ChannelSettings:Capacity` in `appsettings.json` and restart, or override it
for a single run without touching the file:

```bash
# PowerShell
$env:ChannelSettings__Capacity="3"; dotnet run

# bash
ChannelSettings__Capacity=3 dotnet run
```

## Running it

Requires the .NET 10 SDK.

```bash
cd OrderNotificationDemo
dotnet run
```

Swagger UI opens at **http://localhost:5000/swagger** — use it to fire requests
from the browser, or use curl (see below). Watch the console: every queued
order and every sent email is logged with a timestamp and color.

## Endpoints

| Method | Route                     | What it does                                                              |
|--------|---------------------------|----------------------------------------------------------------------------|
| POST   | `/api/orders`             | Queues one order's email on the channel, returns instantly                |
| POST   | `/api/orders/burst?count=10` | Fires N orders back-to-back — watch the queue fill and backpressure kick in |
| POST   | `/api/orders/slow`        | Same order flow, but sends the email inline (no channel) — blocks ~2s     |
| GET    | `/api/orders/channel-status` | Current `Reader.Count` / capacity and whether `Reader.Completion` is done |

### curl examples

```bash
# Single order — returns in a few ms, email is sent in the background
curl -X POST http://localhost:5000/api/orders -H "Content-Type: application/json" -d '{"name":"Akash","email":"akash@test.com"}'

# 10 individual requests in a loop — at the default capacity (500) every one
# returns in milliseconds, since the channel never fills up
for i in $(seq 1 10); do
  curl -s -w " (%{time_total}s)\n" -o /dev/null -X POST http://localhost:5000/api/orders \
    -H "Content-Type: application/json" -d "{\"name\":\"User$i\",\"email\":\"user$i@test.com\"}"
done

# Burst test — fires N orders server-side in one call; a quick way to fill the queue
curl -X POST "http://localhost:5000/api/orders/burst?count=10"

# Check how many emails are queued right now
curl http://localhost:5000/api/orders/channel-status

# Comparison — same order, but WITHOUT a channel (blocks ~2s before responding)
curl -X POST http://localhost:5000/api/orders/slow -H "Content-Type: application/json" -d '{"name":"Akash","email":"akash@test.com"}'
```

## Suggested demo flow

1. **Baseline:** call `/api/orders/slow` once — point out the ~2s wait before the response comes back.
2. **With a channel:** call `/api/orders` — same order, response comes back in milliseconds.
3. **Fast at a roomy capacity:** with the default capacity (500), run the 10-request curl loop
   above — every single request returns in milliseconds, since the channel never fills up.
4. **Backpressure:** stop the app, restart with a small capacity
   (`ChannelSettings__Capacity=3 dotnet run`), then call `/api/orders/burst?count=10` and watch
   the console — the first 3 orders queue instantly, then producers start waiting for the
   worker to catch up. Poll `/api/orders/channel-status` mid-burst to see the queue depth.
