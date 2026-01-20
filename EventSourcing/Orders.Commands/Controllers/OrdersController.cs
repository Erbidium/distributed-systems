using Microsoft.AspNetCore.Mvc;
using NATS.Client.Core;
using NATS.Client.JetStream;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace Orders.Commands.Controllers;

[ApiController]
[Route("orders")]
public class OrdersController : ControllerBase
{
    private static readonly ConcurrentDictionary<Guid, long> _seq = new();
    private readonly ILogger<OrdersController> _logger;
    private readonly Task<(NatsConnection nats, NatsJSContext js, string stream, string subject)> _deps;

    public OrdersController(
        ILogger<OrdersController> logger,
        Task<(NatsConnection, NatsJSContext, string, string)> deps)
    {
        _logger = logger;
        _deps = deps;
    }

    [HttpPost]
    public async Task<IActionResult> Create()
    {
        var orderId = Guid.NewGuid();
        var seq = _seq.AddOrUpdate(orderId, 1, (_, s) => s + 1);

        var ev = Make(orderId, seq, "OrderCreated", new { Status = "Created" });

        var (_, js, _, subject) = await _deps;
        var sw = Stopwatch.StartNew();

        await js.PublishAsync(subject, JsonSerializer.SerializeToUtf8Bytes(ev));

        sw.Stop();
        _logger.LogInformation("Published {Type} {Id} in {Ms}ms", ev.Type, ev.AggregateId, sw.ElapsedMilliseconds);

        return Ok(new { orderId });
    }

    [HttpPost("{id:guid}/confirm")]
    public async Task<IActionResult> Confirm(Guid id)
    {
        var seq = _seq.AddOrUpdate(id, 1, (_, s) => s + 1);
        var ev = Make(id, seq, "OrderConfirmed", new { Status = "Confirmed" });

        var (_, js, _, subject) = await _deps;
        await js.PublishAsync(subject, JsonSerializer.SerializeToUtf8Bytes(ev));

        return Accepted(new { id });
    }

    private static EventEnvelope Make(Guid aggId, long seq, string type, object payload)
    {
        var json = JsonSerializer.SerializeToElement(payload);
        return new EventEnvelope(Guid.NewGuid(), aggId, seq, type, DateTimeOffset.UtcNow, json);
    }
}

