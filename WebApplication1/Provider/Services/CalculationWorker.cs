using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Contracts;
using System.Diagnostics;
using System.Text.Json;

namespace Provider.API.Services;

public sealed class CalculationWorker(ILogger<CalculationWorker> logger) : BackgroundService
{
    private readonly ILogger<CalculationWorker> _logger = logger;
    private IConnection? _connection;
    private IChannel? _channel;

    private readonly ConnectionFactory _factory = new()
    {
        HostName = "rabbitmq",
        Port = 5672,
        UserName = "guest",
        Password = "guest",
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        for (var attempt = 1; attempt <= 10; attempt++)
        {
            try
            {
                _connection = await _factory.CreateConnectionAsync(stoppingToken);
                _channel = await _connection.CreateChannelAsync(null, stoppingToken);
                break;
            }
            catch (Exception ex) when (attempt < 10)
            {
                _logger.LogWarning(ex, "RabbitMQ not ready. Retry {Attempt}/10", attempt);
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }

        await _channel.QueueDeclareAsync(
            queue: "calculation_queue",
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: stoppingToken);

        await _channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 1,
            global: false,
            cancellationToken: stoppingToken);
        
        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += async (_, args) =>
        {
            var sw = Stopwatch.StartNew();

            var message = JsonSerializer.Deserialize<CalculationMessage>(
                args.Body.ToArray())!;

            await Task.Delay(Random.Shared.Next(200, 500), stoppingToken);
            var result = message.A + message.B;

            sw.Stop();

            _logger.LogInformation(
                "Provider [{Instance}] processed {RequestId} in {Time} ms",
                Environment.MachineName,
                message.RequestId,
                sw.ElapsedMilliseconds);

            await _channel.BasicAckAsync(
                args.DeliveryTag,
                multiple: false,
                cancellationToken: stoppingToken);
        };

        await _channel.BasicConsumeAsync(
            queue: "calculation_queue",
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
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