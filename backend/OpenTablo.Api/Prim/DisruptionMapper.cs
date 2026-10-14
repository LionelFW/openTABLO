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
            .ToDictionary(d => d.Id, d => new DisruptionDto(d.Id, d.Severity, d.Cause, d.Title, d.ShortMessage, d.Message));

        return (bulk.Lines ?? [])
            .Select(l => (
                LineId: l.Id.Replace(LinePrefix, string.Empty),
                Disruptions: (l.ImpactedObjects ?? [])
                    .SelectMany(o => o.DisruptionIds ?? [])
                    .Distinct()
                    .Where(active.ContainsKey)
                    .Select(id => active[id])
                    .ToList()))
            .Where(l => l.Disruptions.Count > 0)
            .ToDictionary(l => l.LineId, l => l.Disruptions);
    }

    static bool IsActive(ApplicationPeriod period, DateTime parisNow) =>
        Parse(period.Begin) is { } begin && begin <= parisNow && parisNow <= (Parse(period.End) ?? DateTime.MaxValue);

    static DateTime? Parse(string? value) =>
        DateTime.TryParseExact(value, "yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;
}
