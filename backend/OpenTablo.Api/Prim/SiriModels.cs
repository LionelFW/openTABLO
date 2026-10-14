namespace OpenTablo.Api.Prim;

// SIRI Lite stop-monitoring payload, reduced to the fields openTABLO uses.

public sealed record SiriValue(string? Value);

public sealed record SiriEnvelope(SiriBody? Siri);

public sealed record SiriBody(SiriServiceDelivery? ServiceDelivery);

public sealed record SiriServiceDelivery(IReadOnlyList<StopMonitoringDelivery>? StopMonitoringDelivery);

public sealed record StopMonitoringDelivery(IReadOnlyList<MonitoredStopVisit>? MonitoredStopVisit);

public sealed record MonitoredStopVisit(MonitoredVehicleJourney? MonitoredVehicleJourney);

public sealed record MonitoredVehicleJourney(
    SiriValue? LineRef,
    IReadOnlyList<SiriValue>? DestinationName,
    MonitoredCall? MonitoredCall);

public sealed record MonitoredCall(
    bool? VehicleAtStop,
    IReadOnlyList<SiriValue>? DestinationDisplay,
    DateTimeOffset? ExpectedArrivalTime,
    DateTimeOffset? ExpectedDepartureTime,
    DateTimeOffset? AimedDepartureTime,
    string? DepartureStatus,
    SiriValue? ArrivalPlatformName);
