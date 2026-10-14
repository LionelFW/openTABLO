using OpenTablo.Api.Contracts;

namespace OpenTablo.Api.Prim;

public static class SiriMapper
{
    const int DeparturesPerDestination = 3;
    static readonly TimeSpan Horizon = TimeSpan.FromHours(2);

    /// <summary>"STIF:Line::C01371:" → "C01371".</summary>
    public static string LineId(string? lineRef) =>
        (lineRef ?? string.Empty).Replace("STIF:Line::", string.Empty).Trim(':');

    /// <summary>Groups visits TABLO-style: one row per line and destination with the next few departures.</summary>
    public static IReadOnlyList<DepartureGroupDto> ToDepartureGroups(
        IEnumerable<MonitoredStopVisit> visits, IReadOnlyDictionary<string, LineDto> lines, DateTimeOffset now) =>
        visits
            .Select(v => v.MonitoredVehicleJourney)
            .OfType<MonitoredVehicleJourney>()
            .Select(j => new
            {
                LineId = LineId(j.LineRef?.Value),
                Destination = j.MonitoredCall?.DestinationDisplay?.FirstOrDefault()?.Value
                    ?? j.DestinationName?.FirstOrDefault()?.Value
                    ?? string.Empty,
                Departure = new DepartureDto(
                    j.MonitoredCall?.ExpectedDepartureTime ?? j.MonitoredCall?.ExpectedArrivalTime ?? j.MonitoredCall?.AimedDepartureTime,
                    j.MonitoredCall?.VehicleAtStop ?? false,
                    j.MonitoredCall?.DepartureStatus,
                    j.MonitoredCall?.ArrivalPlatformName?.Value),
            })
            .Where(x => x.Departure.Expected is null || x.Departure.Expected <= now + Horizon)
            .GroupBy(x => (x.LineId, x.Destination))
            .Select(g => new DepartureGroupDto(
                g.Key.LineId,
                lines.GetValueOrDefault(g.Key.LineId),
                g.Key.Destination,
                g.Select(x => x.Departure).OrderBy(d => d.Expected ?? DateTimeOffset.MaxValue).Take(DeparturesPerDestination).ToList()))
            .OrderBy(g => ModeOrder.Of(g.Line?.Mode))
            .ThenBy(g => g.Line?.Name ?? g.LineId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.Next.FirstOrDefault()?.Expected)
            .ToList();
}
