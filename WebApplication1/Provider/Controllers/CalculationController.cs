using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts;
using Shared.Grpc;
using System.Diagnostics;

namespace Provider.Controllers;

[ApiController]
[Route("api/calculate")]
[Authorize]
public class CalculationController : ControllerBase
{
    private readonly ILogger<CalculationController> _logger;

    public CalculationController(ILogger<CalculationController> logger)
    {
        _logger = logger;
    }

    [HttpPost]
    public IActionResult Calculate(CalculationRequest request)
    {
        var sw = Stopwatch.StartNew();

        Thread.Sleep(Random.Shared.Next(100, 500));
        var result = request.A + request.B;

        sw.Stop();

        _logger.LogInformation(
            "Provider calculation time: {Time} ms", sw.ElapsedMilliseconds);

        return Ok(new CalculationResponse
        {
            Result = result,
            ExecutionTimeMs = sw.ElapsedMilliseconds
        });
    }
}

