namespace Shared.Contracts;

public class CalculationResultMessage
{
    public Guid RequestId { get; set; }
    public int Result { get; set; }
    public long ExecutionTimeMs { get; set; }
}

