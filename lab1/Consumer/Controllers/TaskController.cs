using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

[ApiController]
[Route("[controller]")]
public class TaskController : ControllerBase
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<TaskController> _logger;

    public TaskController(IHttpClientFactory httpFactory, ILogger<TaskController> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public record TaskRequest(int n);
    public record ProviderResponse(long Result, long ProcessingMs);
    public record TaskResponse(long result, long providerMs, long totalMs);

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] TaskRequest request)
    {
        var swTotal = Stopwatch.StartNew();
        var client = _httpFactory.CreateClient("provider");

        var httpResponse = await client.PostAsJsonAsync("compute", new { n = request.n });
        httpResponse.EnsureSuccessStatusCode();

        var provider = await httpResponse.Content.ReadFromJsonAsync<ProviderResponse>();

        swTotal.Stop();
        _logger.LogInformation("Consumer: n={N} providerMs={ProviderMs} totalMs={TotalMs}",
            request.n, provider?.ProcessingMs, swTotal.ElapsedMilliseconds);

        var resp = new TaskResponse(provider?.Result ?? 0, provider?.ProcessingMs ?? -1, swTotal.ElapsedMilliseconds);
        return Ok(resp);
    }
}
