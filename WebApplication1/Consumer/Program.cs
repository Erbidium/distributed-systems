using Shared;
using Shared.Grpc;
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

builder.Logging.AddConsole();

var app = builder.Build();
app.MapControllers();
app.Run();
