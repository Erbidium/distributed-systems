using Grpc.Core;
using Shared.Grpc;
using System.Diagnostics;

namespace Provider.Services;

public class GrpcCalculatorService : Calculator.CalculatorBase
{
    private readonly ILogger<GrpcCalculatorService> _logger;

    public GrpcCalculatorService(ILogger<GrpcCalculatorService> logger)
    {
        _logger = logger;
    }

    public override Task<CalcReply> Calculate(CalcRequest request, ServerCallContext context)
    {
        var sw = Stopwatch.StartNew();
        Thread.Sleep(Random.Shared.Next(100, 500));

        var result = request.A + request.B;
        sw.Stop();

        _logger.LogInformation("gRPC calculation time: {Time} ms", sw.ElapsedMilliseconds);

        return Task.FromResult(new CalcReply
        {
            Result = result,
            ExecutionTimeMs = sw.ElapsedMilliseconds
        });
    }
}

