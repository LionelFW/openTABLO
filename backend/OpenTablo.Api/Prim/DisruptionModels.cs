namespace OpenTablo.Api.Prim;

// PRIM "disruptions_bulk/disruptions/v2" payload. Dates are Paris local time formatted yyyyMMddTHHmmss.

public sealed record DisruptionsBulk(
    IReadOnlyList<Disruption>? Disruptions,
    IReadOnlyList<DisruptedLine>? Lines);

public sealed record Disruption(
    string Id,
    IReadOnlyList<ApplicationPeriod>? ApplicationPeriods,
    string? Cause,
    string? Severity,
    string? Title,
    string? ShortMessage,
    string? Message);

public sealed record ApplicationPeriod(string? Begin, string? End);

public sealed record DisruptedLine(string Id, IReadOnlyList<ImpactedObject>? ImpactedObjects);

public sealed record ImpactedObject(string? Type, string? Id, IReadOnlyList<string>? DisruptionIds);
