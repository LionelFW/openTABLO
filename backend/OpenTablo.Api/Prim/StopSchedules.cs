using System.Globalization;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using OpenTablo.Api.Contracts;
using OpenTablo.Api.Data;

namespace OpenTablo.Api.Prim;

// Navitia stop_schedules, limited to the fields used.
public sealed record StopSchedulesResponse([property: JsonPropertyName("stop_schedules")] List<StopSchedule>? StopSchedules);
public sealed record StopSchedule(
    [property: JsonPropertyName("route")] NavitiaRoute? Route,
    [property: JsonPropertyName("date_times")] List<NavitiaDateTime>? DateTimes);
public sealed record NavitiaRoute([property: JsonPropertyName("line")] NavitiaLine? Line);
public sealed record NavitiaLine([property: JsonPropertyName("id")] string? Id);
public sealed record NavitiaDateTime([property: JsonPropertyName("date_time")] string? DateTime);

/// <summary>A record, not a tuple: HybridCache round-trips values through JSON, which skips tuple fields.</summary>
public sealed record PlannedTimes(string LineId, List<DateTimeOffset> Planned);

/// <summary>
/// Next planned departure per line, for lines with nothing in the real-time window (night buses, first trains).
/// Planned times are stable, so one Navitia call per interchange zone is cached for a long time.
/// </summary>
public sealed class ScheduleService(PrimClient prim, TabloDbContext db, HybridCache cache, TimeProvider clock)
{
    static readonly TimeZoneInfo Paris = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");
    static readonly TimeSpan Ttl = TimeSpan.FromMinutes(20);

    /// <param name="lineIds">Lines wanted, among the stop's own; all of them when empty.</param>
    public async Task<IReadOnlyList<ScheduledDto>> GetAsync(string stopAreaId, IReadOnlyCollection<string> lineIds, CancellationToken ct)
    {
        var stop = await db.StopAreas.AsNoTracking().Where(s => s.Id == stopAreaId)
            .Select(s => new { s.ZdcId, LineIds = s.Lines.Select(l => l.Id).ToList() })
            .FirstOrDefaultAsync(ct);
        if (stop?.ZdcId is null) return [];

        var zone = await cache.GetOrCreateAsync(
            $"schedules:{stop.ZdcId}",
            async token => NextByLine(await prim.GetStopSchedulesAsync(stop.ZdcId, token)),
            new HybridCacheEntryOptions { Expiration = Ttl, LocalCacheExpiration = Ttl },
            cancellationToken: ct);

        // The zone also holds other stops' lines; cached times may have passed since.
        var now = clock.GetUtcNow();
        return zone.Where(s => stop.LineIds.Contains(s.LineId) && (lineIds.Count == 0 || lineIds.Contains(s.LineId)))
            .Select(s => (s.LineId, Next: s.Planned.FirstOrDefault(t => t > now)))
            .Where(s => s.Next != default)
            .Select(s => new ScheduledDto(s.LineId, s.Next))
            .ToList();
    }

    /// <summary>All planned times per line ("line:IDFM:C01104" → C01104), earliest first, merged across routes and directions.</summary>
    public static List<PlannedTimes> NextByLine(StopSchedulesResponse? response) =>
        (response?.StopSchedules ?? [])
            .Where(s => s.Route?.Line?.Id is not null)
            .GroupBy(s => s.Route!.Line!.Id!.Split(':')[^1])
            .Select(g => new PlannedTimes(g.Key, g.SelectMany(s => s.DateTimes ?? []).Select(d => ParseParis(d.DateTime)).OfType<DateTimeOffset>().Order().ToList()))
            .Where(x => x.Planned.Count > 0)
            .ToList();

    /// <summary>Navitia times are Paris local time without offset, e.g. 20261015T003000.</summary>
    public static DateTimeOffset? ParseParis(string? value)
    {
        if (!DateTime.TryParseExact(value, "yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var local)) return null;
        return new DateTimeOffset(local, Paris.GetUtcOffset(local));
    }
}
