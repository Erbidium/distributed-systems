using System.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient("provider", c =>
{
    c.BaseAddress = new Uri("http://provider");
    c.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", JwtHelper.Generate());
});

builder.Services.AddGrpcClient<Calculator.CalculatorClient>(o =>
{
    o.Address = new Uri("http://provider");
});

builder.Logging.AddConsole();

var app = builder.Build();
app.MapControllers();
app.Run();
