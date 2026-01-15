using Microsoft.AspNetCore.Mvc;
using Shared.Contracts;
using Shared.Grpc;
using System.Diagnostics;

namespace Consumer.API.Controllers;

[ApiController]
[Route("api/process")]
public class ProcessController : ControllerBase
{
    private readonly IHttpClientFactory _factory;
    private readonly ILogger<ProcessController> _logger;
    private readonly Calculator.CalculatorClient _grpc;

    public ProcessController(
        IHttpClientFactory factory,
        Calculator.CalculatorClient grpc,
        ILogger<ProcessController> logger)
    {
        _factory = factory;
        _grpc = grpc;
        _logger = logger;
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
}

