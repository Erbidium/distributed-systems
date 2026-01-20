using Consumer.API.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared;
using Shared.Contracts;
using Shared.Grpc;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;

namespace Consumer.API.Controllers;

[ApiController]
[Route("api/process")]
public class ProcessController : ControllerBase
{
    private readonly IHttpClientFactory _factory;
    private readonly ILogger<ProcessController> _logger;
    private readonly Calculator.CalculatorClient _grpc;
    private readonly RabbitPublisher _publisher;
    private readonly ConcurrentDictionary<Guid, Stopwatch> _pendingRequests;

    public ProcessController(
        IHttpClientFactory factory,
        Calculator.CalculatorClient grpc,
        ILogger<ProcessController> logger,
        RabbitPublisher publisher,
        ConcurrentDictionary<Guid, Stopwatch> pendingRequests)
    {
        _factory = factory;
        _grpc = grpc;
        _logger = logger;
        _publisher = publisher;
        _pendingRequests = pendingRequests;
    }

    [HttpGet("token")]
    [AllowAnonymous]
    public string GetToken()
    {
        return JwtHelper.Generate();
    }

    [HttpGet("rest")]
    public async Task<IActionResult> ProcessRest()
    {
        var sw = Stopwatch.StartNew();
        var client = _factory.CreateClient("provider");

        var req = new CalculationRequest { A = 5, B = 7 };
        var response = await client.PostAsJsonAsync("/api/calculate", req);
        var result = await response.Content.ReadFromJsonAsync<CalculationResponse>();

        sw.Stop();
        _logger.LogInformation("Consumer REST time: {Time} ms", sw.ElapsedMilliseconds);

        return Ok(new { result, TotalTimeMs = sw.ElapsedMilliseconds });
    }

    [HttpGet("grpc")]
    public async Task<IActionResult> ProcessGrpc()
    {
        var sw = Stopwatch.StartNew();

        var reply = await _grpc.CalculateAsync(new CalcRequest { A = 5, B = 7 });

        sw.Stop();
        _logger.LogInformation("Consumer gRPC time: {Time} ms", sw.ElapsedMilliseconds);

        return Ok(new { reply.Result, TotalTimeMs = sw.ElapsedMilliseconds });
    }

    [HttpPost("async")]
    public async Task<IActionResult> SendAsync()
    {
        var requestId = Guid.NewGuid();
        var sw = Stopwatch.StartNew();

        _pendingRequests[requestId] = sw;

        var message = new CalculationMessage
        {
            RequestId = requestId,
            A = 5,
            B = 7,
            CreatedAt = DateTime.UtcNow
        };

        var body = JsonSerializer.SerializeToUtf8Bytes(message);

        await _publisher.PublishAsync(body);

        return Accepted(new { requestId });
    }
}

