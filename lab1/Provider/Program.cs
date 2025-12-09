using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var app = builder.Build();
app.MapControllers();

// Make Kestrel listen on port 80 inside container (useful for docker)
app.Urls.Add("http://*:80");

app.Run();
