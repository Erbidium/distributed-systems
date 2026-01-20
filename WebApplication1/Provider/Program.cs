using Microsoft.AspNetCore.Server.Kestrel.Core;
using Provider.API.Services;
using Provider.Services;
using Shared;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddGrpc();
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", opt =>
    {
        opt.RequireHttpsMetadata = false;
        opt.TokenValidationParameters = JwtHelper.Parameters;
    });
builder.Services.Configure<HostOptions>(o =>
{
    o.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
});
builder.Services.AddHostedService<CalculationWorker>();
builder.Services.AddAuthorization();
builder.Logging.AddConsole();

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8080, o =>
    {
        o.Protocols = HttpProtocols.Http1; // REST
    });

    options.ListenAnyIP(8081, o =>
    {
        o.Protocols = HttpProtocols.Http2; // gRPC
    });
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGrpcService<GrpcCalculatorService>();

app.Run();

