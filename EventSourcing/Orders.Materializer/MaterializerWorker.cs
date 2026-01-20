using Cassandra;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using System.Diagnostics;
using System.IO;
using ISession = Cassandra.ISession;

namespace Orders.Materializer;

public sealed class MaterializerWorker(
    ILogger<MaterializerWorker> logger,
    Task<(ISession, PreparedStatement, PreparedStatement)> db) : BackgroundService
{
    private readonly ILogger<MaterializerWorker> _logger = logger;
    private readonly Task<(ISession session, PreparedStatement upsert, PreparedStatement select)> _db = db;
    private readonly string _natsUrl = Environment.GetEnvironmentVariable("NATS_URL") ?? "nats://localhost:4222";
    private readonly string _stream = Environment.GetEnvironmentVariable("NATS_STREAM") ?? "ORDERS";
    private readonly string _subject = Environment.GetEnvironmentVariable("NATS_SUBJECT") ?? "orders.events";
    private readonly string _durable = Environment.GetEnvironmentVariable("NATS_DURABLE") ?? "orders-materializer";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("started");

        var nats = new NatsConnection(new NatsOpts { Url = _natsUrl });
        var js = new NatsJSContext(nats);

        try
        {
            await js.CreateStreamAsync(new StreamConfig(_stream, [_subject])
            {
                Retention = StreamConfigRetention.Limits,
                Storage = StreamConfigStorage.File,
                MaxAge = TimeSpan.FromDays(7),
            });
            _logger.LogInformation("JetStream stream created: {Stream}", _stream);
        }
        catch (NatsJSApiException ex) when (ex.Message.Contains("stream name already in use", StringComparison.OrdinalIgnoreCase)
                                        || ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("JetStream stream already exists: {Stream}", _stream);
        }

        var consumer = await js.CreateOrUpdateConsumerAsync(
            stream: _stream,
            config: new ConsumerConfig(_durable)
            {
                DurableName = _durable,
                AckPolicy = ConsumerConfigAckPolicy.Explicit,
                DeliverPolicy = ConsumerConfigDeliverPolicy.All,
                FilterSubject = _subject
            },
            cancellationToken: stoppingToken);

        await foreach (var msg in consumer.ConsumeAsync<EventEnvelope>(cancellationToken: stoppingToken))
        {
            var sw = Stopwatch.StartNew();

            try
            {
                var env = msg.Data;
                if (env is null) { await msg.AckAsync(cancellationToken: stoppingToken); continue; }

                await ApplyEventAsync(env, stoppingToken);

                sw.Stop();
                _logger.LogInformation("Applied {Type} {Agg} seq={Seq} in {Ms}ms",
                    env.Type, env.AggregateId, env.Seq, sw.ElapsedMilliseconds);

                await msg.AckAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply event; will NAK");
                await msg.NakAsync(); // повторить пізніше
            }
        }
    }

    private async Task ApplyEventAsync(EventEnvelope ev, CancellationToken ct)
    {
        var (session, upsert, select) = await _db;

        // read last_seq
        var row = (await session.ExecuteAsync(select.Bind(ev.AggregateId))).FirstOrDefault();
        var lastSeq = row is null ? 0 : row.GetValue<long?>("last_seq") ?? 0;

        if (ev.Seq <= lastSeq)
            return; // ідемпотентність

        // projection rules
        var status = row?.GetValue<string>("status") ?? "Unknown";
        var total = row?.GetValue<decimal?>("total") ?? 0m;

        switch (ev.Type)
        {
            case "OrderCreated":
                status = "Created";
                total = 0m;
                break;

            case "OrderConfirmed":
                status = "Confirmed";
                break;

                // додай ItemAdded → total += price*qty
        }

        await session.ExecuteAsync(upsert.Bind(
            ev.AggregateId,
            status,
            total,
            ev.OccurredAt.UtcDateTime,
            ev.Seq
        ));
    }
}

