using System.Globalization;
using OpenTablo.Api.Contracts;

namespace OpenTablo.Api.Prim;

public static class DisruptionMapper
{
    const string LinePrefix = "line:IDFM:";

    /// <summary>Disruptions in effect at <paramref name="parisNow"/>, keyed by IDFM line code (e.g. C01371).</summary>
    public static Dictionary<string, List<DisruptionDto>> ActiveByLine(DisruptionsBulk bulk, DateTime parisNow)
    {
        var active = (bulk.Disruptions ?? [])
            .Where(d => (d.ApplicationPeriods ?? []).Any(p => IsActive(p, parisNow)))
            .ToDictionary(d => d.Id);

        return (bulk.Lines ?? [])
            .Select(l => (
                LineId: l.Id.Replace(LinePrefix, string.Empty),
                Disruptions: (l.ImpactedObjects ?? [])
                    .SelectMany(o => o.DisruptionIds ?? [])
                    .Distinct()
                    .Where(active.ContainsKey)
                    .Select(id => ToDto(active[id], l))
                    .ToList()))
            .Where(l => l.Disruptions.Count > 0)
            .ToDictionary(l => l.LineId, l => l.Disruptions);
    }

    static DisruptionDto ToDto(Disruption d, DisruptedLine line)
    {
        // Stops listed for this line say exactly where; the section is the fallback when PRIM gives none.
        var places = (line.ImpactedObjects ?? [])
            .Where(o => o.Type != "line" && o.Name is not null && (o.DisruptionIds ?? []).Contains(d.Id))
            .Select(o => o.Name!)
            .Distinct()
            .ToList();
        if (places.Count == 0)
            places = (d.ImpactedSections ?? [])
                .Where(s => s.LineId == line.Id && s.From?.Name is not null)
                .Select(s => s.To?.Name is { } to && to != s.From!.Name ? $"{s.From!.Name} → {to}" : s.From!.Name!)
                .Distinct()
                .ToList();
        return new DisruptionDto(d.Id, d.Severity, d.Cause, d.Title, d.ShortMessage, d.Message, places);
    }

    static bool IsActive(ApplicationPeriod period, DateTime parisNow) =>
        Parse(period.Begin) is { } begin && begin <= parisNow && parisNow <= (Parse(period.End) ?? DateTime.MaxValue);

    static DateTime? Parse(string? value) =>
        DateTime.TryParseExact(value, "yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;
}
