using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var providerUrl = Environment.GetEnvironmentVariable("PROVIDER_URL") ?? "http://provider:80/";
builder.Services.AddHttpClient("provider", client =>
{
    client.BaseAddress = new Uri(providerUrl);
});

var app = builder.Build();
app.MapControllers();

app.Urls.Add("http://*:80");

app.Run();
