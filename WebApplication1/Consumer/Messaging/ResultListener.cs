using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Contracts;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace Consumer.API.Messaging;

public sealed class ResultListener : BackgroundService
{
    private readonly ILogger<ResultListener> _logger;
    private readonly ConcurrentDictionary<Guid, Stopwatch> _pendingRequests;

    private IConnection? _connection;
    private IChannel? _channel;

    private readonly ConnectionFactory _factory = new()
    {
        HostName = "rabbitmq",
        Port = 5672,
        UserName = "guest",
        Password = "guest",
    };

    public ResultListener(
        ILogger<ResultListener> logger,
        ConcurrentDictionary<Guid, Stopwatch> pendingRequests)
    {
        _logger = logger;
        _pendingRequests = pendingRequests;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {

        for (var attempt = 1; attempt <= 10; attempt++)
        {
            try
            {
                _connection = await _factory.CreateConnectionAsync(ct);
                _channel = await _connection.CreateChannelAsync(null,ct);
                break;
            }
            catch (Exception ex) when (attempt < 10)
            {
                _logger.LogWarning(ex, "RabbitMQ not ready. Retry {Attempt}/10", attempt);
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
        }

        await _channel.QueueDeclareAsync(
            queue: "calculation_results",
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: ct);

        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += async (_, e) =>
        {
            var result = JsonSerializer.Deserialize<CalculationResultMessage>(
                e.Body.ToArray())!;

            if (_pendingRequests.TryRemove(result.RequestId, out var sw))
            {
                sw.Stop();

                _logger.LogInformation(
                    "Consumer received result for {Id}. Total time: {Time} ms",
                    result.RequestId,
                    sw.ElapsedMilliseconds);
            }

            await _channel.BasicAckAsync(
                e.DeliveryTag,
                multiple: false,
                cancellationToken: ct);
        };

        await _channel.BasicConsumeAsync(
            queue: "calculation_results",
            autoAck: false,
            consumer: consumer,
            cancellationToken: ct);

        await Task.Delay(Timeout.Infinite, ct);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
            await _channel.CloseAsync(cancellationToken);

        if (_connection is not null)
            await _connection.CloseAsync(cancellationToken);

        await base.StopAsync(cancellationToken);
    }
}

