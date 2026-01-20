using System.Text.Json;

namespace Orders.Materializer;

public sealed record EventEnvelope(
    Guid EventId,
    Guid AggregateId,
    long Seq,
    string Type,
    DateTimeOffset OccurredAt,
    JsonElement Payload
);

