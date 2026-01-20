namespace Shared.Contracts;

public class CalculationMessage
{
    public Guid RequestId { get; set; }
    public int A { get; set; }
    public int B { get; set; }
    public DateTime CreatedAt { get; set; }
}

