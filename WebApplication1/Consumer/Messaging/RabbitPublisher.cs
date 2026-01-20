namespace Consumer.API.Messaging;

using RabbitMQ.Client;

public sealed class RabbitPublisher(ILogger<RabbitPublisher> logger) : IAsyncDisposable
{
    private IConnection? _connection;
    private IChannel? _channel;

    private readonly ConnectionFactory _factory = new()
    {
        HostName = "rabbitmq",
        Port = 5672,
        UserName = "guest",
        Password = "guest",
    };

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_connection is not null)
            return;

        for (var attempt = 1; attempt <= 10; attempt++)
        {
            try
            {
                _connection = await _factory.CreateConnectionAsync(ct);
                _channel = await _connection.CreateChannelAsync(null, ct);
                break;
            }
            catch (Exception ex) when (attempt < 10)
            {
                logger.LogWarning(ex, "RabbitMQ not ready. Retry {Attempt}/10", attempt);
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
        }

        await _channel.QueueDeclareAsync(
            queue: "calculation_queue",
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: ct);
    }

    public async Task PublishAsync(byte[] body, CancellationToken ct = default)
    {
        if (_channel is null)
            throw new InvalidOperationException("Publisher not initialized");

        await _channel.BasicPublishAsync(
            exchange: "",
            routingKey: "calculation_queue",
            body: body,
            cancellationToken: ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
            await _channel.CloseAsync();

        if (_connection is not null)
            await _connection.CloseAsync();
    }
}
