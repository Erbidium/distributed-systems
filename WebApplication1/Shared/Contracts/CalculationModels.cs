namespace Shared.Contracts;

public class CalculationRequest
{
    public int A { get; set; }
    public int B { get; set; }
}

public class CalculationResponse
{
    public int Result { get; set; }
    public long ExecutionTimeMs { get; set; }
}
