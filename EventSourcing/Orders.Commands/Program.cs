using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using System.IO;

var natsUrl = Environment.GetEnvironmentVariable("NATS_URL") ?? "nats://localhost:4222";
var stream = Environment.GetEnvironmentVariable("NATS_STREAM") ?? "ORDERS";
var subject = Environment.GetEnvironmentVariable("NATS_SUBJECT") ?? "orders.events";

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSingleton(async sp =>
{
    var nats = new NatsConnection(new NatsOpts { Url = natsUrl });
    var js = new NatsJSContext(nats);

    // ensure stream exists
    try
    {
        await js.CreateStreamAsync(new StreamConfig(stream, [subject])
        {
            Retention = StreamConfigRetention.Limits,
            Storage = StreamConfigStorage.File,
            MaxAge = TimeSpan.FromDays(7),
        });
    }
    catch (NatsJSApiException ex) when (ex.Message.Contains("stream name already in use", StringComparison.OrdinalIgnoreCase)
                                    || ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
    { }

    return (nats, js, stream, subject);
});

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
