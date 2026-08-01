# Presenter Script — "Channels in .NET" (channels-in-dotnet.pptx)

Total runtime: **~45–50 minutes** (17 slides + live demo). Each section below maps
to one slide. **Say:** is what to speak. **Do:** is what to click/type. **Show:**
is what the audience should be looking at.

Terminal prep before you start: have two terminals open in
`dotnet-channels-demo/OrderNotificationDemo` — one for `dotnet run`, one for
curl commands. Have Swagger (`http://localhost:5000/swagger`) bookmarked.

---

## Slide 1 — Title (~1 min)

**Say:**
> "Today we're talking about `System.Threading.Channels` — async producer-consumer
> patterns for high-performance .NET applications. By the end of this session
> you'll have seen it used in a real ASP.NET Core API, not just a toy console app."

---

## Slide 2 — Agenda (~1 min)

**Say:**
> "Here's the plan: what a channel actually is, then we build a hands-on
> project — an e-commerce order notification system — then we talk about when
> to reach for this versus other tools, the honest trade-offs, some performance
> numbers, and finally we run the whole thing live together."

---

## Slide 3 — What is a Channel? (~3 min)

**Say:**
> "A Channel is a thread-safe, async pipe between code that produces data and
> code that consumes it. One side calls `channel.Writer` to put items in,
> the other calls `channel.Reader` to take items out. It's FIFO, it's
> thread-safe, and critically — it's fully async. No manual locks, no
> `Monitor`, no `SemaphoreSlim`, no race conditions to reason about."

**Point at the diagram:** Producer → Writer → Channel<T> → Reader → Consumer.

---

## Slide 4 — The simplest Channel code (~3 min)

**Say:**
> "Three lines is really all it takes. Create the pipe with
> `Channel.CreateUnbounded<string>()`. Write items in with
> `WriteAsync`. Read them out with `await foreach` over
> `Reader.ReadAllAsync()` — that loop just keeps running, pulling items as
> they arrive. Call `Writer.Complete()` when you're done producing — that's
> what tells the reader's `await foreach` to end. Forget that call, and your
> consumer waits forever."

---

## Slide 5 — Bounded vs unbounded (~4 min)

**Say:**
> "`CreateUnbounded` has no capacity limit — the writer never waits, but if
> your consumer falls behind, memory grows without bound. That's fine for a
> quick console demo, but in production it's a ticking `OutOfMemoryException`.
>
> `CreateBounded(n)` puts a hard cap on the queue. Once it's full, the writer
> *pauses* — that's backpressure, and it's why you always use bounded channels
> in production: predictable memory, automatic flow control.
>
> When the channel is full, `BoundedChannelFullMode` decides what happens:
> `Wait` — the default — pauses the producer until a slot opens. `DropOldest`
> and `DropNewest` evict an item instead of waiting. `DropWrite` silently
> discards the new item. We use `Wait` in our demo today, because we *want*
> to see producers pause."

---

## Slide 6 — Hands-on project: the problem/solution (~3 min)

**Say:**
> "Here's the real scenario. A user places an order. The API sends a
> confirmation email *before* responding — and sending an email takes a
> couple of seconds. At 500 concurrent requests, that's 500 threads blocked
> on email I/O. The thread pool starves, and the whole API stops responding
> to everyone — not just the people placing orders.
>
> The fix: the API drops the email onto a `Channel<T>` and returns instantly —
> tens of milliseconds. A separate `BackgroundService` reads from the channel
> and sends emails at its own pace. API threads are never blocked on I/O."

---

## Slide 7 — Project architecture (~3 min)

**Say:**
> "This is the actual project structure we're about to run.
> `OrderController` writes to the channel. `EmailWorker`, a `BackgroundService`,
> reads from it. The channel is registered once, as a singleton, so both sides
> share the exact same instance via DI. Capacity here is configurable — we'll
> use that in a minute to show two different behaviors from the same code."

**Do:** Have `Controllers/`, `Models/`, `Services/`, and `Program.cs` open in your
editor's file tree so the audience sees the real files.

---

## Slide 8 — OrderController: the fast side (~3 min)

**Say:**
> "`WriteAsync` on a channel with room completes in nanoseconds — that's the
> whole trick. The controller builds an order ID, drops an `EmailMessage`
> onto the channel, and returns `200 OK` immediately. It has zero knowledge
> of how or when the email actually gets sent."

**Do:** Open `Controllers/OrderController.cs`, point at the `PlaceOrder` method.

---

## Slide 9 — EmailWorker: the slow side (~3 min)

**Say:**
> "On the other side, `EmailWorker` is a `BackgroundService`. It does
> `await foreach` over `channel.Reader.ReadAllAsync()` — that loop blocks
> asynchronously until an item shows up, processes it, then waits for the
> next one. Notice the try/catch around the send: that's essential. If one
> email throws and you don't catch it, the exception kills `ExecuteAsync`,
> the loop ends, and the channel silently stops being drained — every order
> after that point queues forever with nothing reading it."

**Do:** Open `Services/EmailWorker.cs`, point at the try/catch.

---

## Slide 10 — Why use Channels? (~3 min)

**Say:**
> "Four reasons. One: it decouples fast work from slow work — the API responds
> instantly, slow work happens in the background, neither blocks the other.
> Two: the thread pool stays healthy, because threads return to the pool
> immediately instead of sitting blocked on I/O. Three: bounded channels give
> you flow control for free — no custom throttling code. Four: it's zero
> infrastructure — it's part of the .NET runtime. No Redis, no RabbitMQ, no
> extra NuGet packages."

---

## Slide 11 — When to use (and when not to) (~3 min)

**Say:**
> "Use channels for background job queues inside one process, batching writes,
> producer-consumer patterns, rate limiting, decoupling a fast API from slow
> I/O — all *inside* one process.
>
> Don't reach for a channel for cross-process communication, guaranteed
> delivery, broadcasting to multiple consumers, durable queues that survive a
> restart, or talking between microservices. For that you want RabbitMQ,
> Kafka, or Azure Service Bus. The rule of thumb: Channels work *inside* one
> service. Message brokers work *between* services. Most real systems use
> both — a channel inside a service, a broker at the service boundary."

---

## Slide 12 — Advantages (~2 min)

**Say:**
> "Quick highlights: zero-allocation fast path when there's room in the
> buffer, no manual locking, fully async — no blocked threads — built-in
> backpressure on bounded channels, clean separation between producer and
> consumer, and it's lightweight: no infrastructure, no config."

---

## Slide 13 — Disadvantages (~2 min)

**Say:**
> "To be fair to the trade-offs: it's in-memory only, so a crash loses
> everything queued. It's single-process — you can't send across machines.
> There's no built-in retry — a failed item is just gone unless you code
> retry yourself. No dashboard or queue-depth metric out of the box — that's
> why we built our own `/channel-status` endpoint today. Forgetting
> `Complete()` hangs your consumer silently. And an unbounded channel with a
> fast producer is a memory leak waiting to happen."

---

## Slide 14 — Performance impact (~3 min)

**Say:**
> "Some concrete numbers. API response time: roughly 60x faster — from about
> 3 seconds down to about 50 milliseconds. Threads blocked per request: from
> N down to zero. Memory per write: essentially zero allocation, because
> `WriteAsync` on the fast path returns a cached `ValueTask`.
>
> Why this matters under load: without a channel, 500 concurrent requests
> means 500 threads blocked on email I/O — the thread pool starves and new
> requests start timing out. With a channel, those same 500 requests return
> instantly, and only one background worker thread is doing the actual
> email work. If you have a single producer and single consumer, you can
> also set `SingleReader`/`SingleWriter` hints on `BoundedChannelOptions` —
> that lets the runtime skip some internal synchronization for even less
> overhead."

---

## Slide 15 — Live demo (~12–15 min)

This is the interactive part — see the **Live Demo Runbook** section below for
exact commands and expected output. Talk through what you're about to do
*before* you do it, so the audience knows what to watch for.

---

## Slide 16 — Key takeaways (~2 min)

**Say:**
> "Five things to remember. `Channel<T>` is a thread-safe async pipe —
> producer writes, consumer reads. Always use `CreateBounded` in production.
> Call `Complete()` when producers are done, or your consumer hangs forever.
> Channels work *inside* a service — use a message broker *between* services.
> And performance-wise: zero-allocation fast path, no thread blocking, and
> built-in backpressure, all for free from the .NET runtime."

---

## Slide 17 — Thank you (~1 min)

**Say:**
> "That's `System.Threading.Channels`. Docs are at
> docs.microsoft.com/dotnet/core/extensions/channels, and the runtime source
> is on GitHub under dotnet/runtime. Happy to take questions, and the full
> demo project is up at github.com/Akash-Rana-simform/dotnet-channels-demo
> if you want to run it yourself afterward."

---

# Live Demo Runbook (for Slide 15)

Run every command from `dotnet-channels-demo/OrderNotificationDemo`. All
commands below are verified working against the actual project.

### Step 0 — Start the app

**Say:** "Let's run the project we just looked at."

**Do:**
```bash
dotnet run
```

**Show:** the startup banner, and the `[EmailWorker] Started — waiting for
emails on the channel...` line. Open `http://localhost:5000/swagger` in a
browser tab so it's ready.

---

### Step 1 — One order, with the channel (fast)

**Say:** "Watch the response time — this should come back almost instantly,
even though sending the email takes 2 seconds."

**Do:**
```bash
curl -X POST http://localhost:5000/api/orders -H "Content-Type: application/json" -d '{"name":"Akash","email":"akash@test.com"}'
```

**Show:** the JSON response `{ "orderId": "...", "status": "Confirmed" }`
comes back immediately. Then point at the console: a few seconds later,
`📧 Sending email to akash@test.com...` then `✅ Email sent... (took ~2000ms)`
appears on its own.

---

### Step 2 — Ten individual requests, still fast (default capacity 500)

**Say:** "Now let's fire ten of these back to back. At the default capacity —
500 — the channel never fills up, so every single one should come back fast."

**Do (bash):**
```bash
for i in $(seq 1 10); do
  curl -s -w " (%{time_total}s)\n" -o /dev/null -X POST http://localhost:5000/api/orders \
    -H "Content-Type: application/json" -d "{\"name\":\"User$i\",\"email\":\"user$i@test.com\"}"
done
```

**Do (PowerShell, if presenting on Windows):**
```powershell
1..10 | ForEach-Object {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    Invoke-RestMethod -Uri "http://localhost:5000/api/orders" -Method Post -ContentType "application/json" `
        -Body "{`"name`":`"User$_`",`"email`":`"user$_@test.com`"}" | Out-Null
    "$($sw.ElapsedMilliseconds)ms"
}
```

**Show:** all ten timings are in the single- or low-double-digit milliseconds.
Then hit the status endpoint to show the queue draining in the background:

```bash
curl http://localhost:5000/api/orders/channel-status
```

**Say:** "Ten orders, all instant, because our channel has plenty of room —
capacity 500. The emails are still queued up and sending one at a time in
the background, two seconds apart."

---

### Step 3 — Shrink the capacity, show backpressure

**Say:** "Now let's make the channel small on purpose, so we can *see*
backpressure happen instead of just hearing about it."

**Do:** Stop the app (`Ctrl+C`), then restart with a small capacity:

```bash
# bash
ChannelSettings__Capacity=3 dotnet run

# PowerShell
$env:ChannelSettings__Capacity="3"; dotnet run
```

**Do:** Fire the burst endpoint, which queues 10 orders in one call:

```bash
curl -X POST "http://localhost:5000/api/orders/burst?count=10"
```

**Show:** this call now takes several seconds to return (roughly 2s × the
number of orders past capacity), because `WriteAsync` is *pausing* once the
3-slot buffer fills. Point at the console log lines — `Queue: 3/3` repeats
while the worker catches up one email at a time.

**Say:** "That pause you just watched *is* backpressure. The producer isn't
allowed to pile up unbounded work — it waits for the consumer to make room.
No custom throttling code, no semaphore, no rate limiter. That's the channel
itself protecting the process."

---

### Step 4 — Compare: without a channel at all

**Say:** "Last comparison. This endpoint does the exact same order flow, but
skips the channel entirely — it awaits the email send inline before
responding."

**Do:**
```bash
curl -X POST http://localhost:5000/api/orders/slow -H "Content-Type: application/json" -d '{"name":"Akash","email":"akash@test.com"}'
```

**Show:** the response takes ~2 seconds to come back, and includes
`elapsedMs` in the JSON confirming it.

**Say:** "Same order, same email, but now the caller pays the full cost of
the slow work. That's the difference a channel makes — not in what work
gets done, but in who has to wait for it."

---

### Demo wrap-up line

**Say:** "So to recap what we just saw live: one order is instant, ten orders
are instant at a healthy capacity, a small capacity visibly makes producers
wait — that's backpressure working as designed — and without a channel at
all, every single request pays the full 2-second cost. Same underlying work,
completely different experience for the caller."

---

## Anticipated Q&A

- **"Why not just use `Task.Run` to fire-and-forget the email instead of a
  channel?"** — `Task.Run` gives you no backpressure (unbounded work can pile
  up), no ordering guarantee, and an unhandled exception there is much easier
  to lose silently. A bounded channel plus one worker gives you a controlled,
  observable pipeline.
- **"What if the process crashes with items still in the channel?"** — They're
  gone. Channels are in-memory only (slide 13). If you need durability across
  restarts, that's a job for a real queue (RabbitMQ, Azure Storage Queues,
  etc.), not `System.Threading.Channels`.
- **"Can multiple workers read from the same channel?"** — Yes, multiple
  consumers can call `ReadAllAsync` concurrently on the same reader; items are
  still delivered to exactly one consumer each (competing consumers), not
  broadcast to all of them.
- **"Does capacity have to be a fixed number?"** — No — ours reads from
  `appsettings.json` / an env var precisely so you can tune it without a
  recompile, which is what Step 3 above demonstrates.
