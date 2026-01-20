using Consumer.API.Messaging;
using Shared;
using Shared.Grpc;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient("provider", c =>
{
    c.BaseAddress = new Uri("http://provider:8080");
    c.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", JwtHelper.Generate());
});

AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

builder.Services.AddGrpcClient<Calculator.CalculatorClient>(o =>
{
    o.Address = new Uri("http://provider:8081");
});

builder.Services.AddSingleton(
    new ConcurrentDictionary<Guid, Stopwatch>());

builder.Services.Configure<HostOptions>(o =>
{
    o.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
});
builder.Services.AddHostedService<ResultListener>();
builder.Services.AddSingleton<RabbitPublisher>();

builder.Logging.AddConsole();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var publisher = scope.ServiceProvider.GetRequiredService<RabbitPublisher>();
    await publisher.InitializeAsync();
}

app.MapControllers();
app.Run();
