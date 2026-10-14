using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;
using OpenTablo.Api.Contracts;
using OpenTablo.Api.Data;
using OpenTablo.Api.Options;
using OpenTablo.Api.Velib;

namespace OpenTablo.Api.Referential;

public sealed class NearbyService(TabloDbContext db, VelibStatusService velibStatus, IOptions<SearchOptions> options, ILogger<NearbyService> logger)
{
    static readonly string[] HeroTypes = ["metro", "rail"];
    // Beyond this a station is no longer "nearby", e.g. a Métro 28 km away from rural Yvelines.
    const int HeroMaxDistance = 5000;
    // Within this, each further Métro or RER line gets its own hero, wherever the radius ends.
    const int HeroLineDistance = 500;

    /// <summary>
    /// Nearest first, keeps a station when it is the first of its mode or serves a line no closer
    /// hero serves, so each line shows once, at its nearest station.
    /// </summary>
    public static List<StopAreaDto> PickHeroes(IEnumerable<StopAreaDto> candidates)
    {
        var heroes = new List<StopAreaDto>();
        var types = new HashSet<string>();
        var lines = new HashSet<string>();
        foreach (var stop in candidates.DistinctBy(s => s.Id).OrderBy(s => s.Distance))
        {
            var newLine = false;
            foreach (var line in stop.Lines) newLine |= lines.Add(line.Id);
            if (types.Add(stop.Type) | newLine) heroes.Add(stop);
        }
        return heroes;
    }

    /// <summary>Signage mode order, nearest first within a mode.</summary>
    public static List<StopAreaDto> ByMode(IEnumerable<StopAreaDto> stops) =>
        stops.OrderBy(s => ModeOrder.Of(s.Type)).ThenBy(s => s.Distance).ToList();

    public int ClampRadius(int? radius) => Math.Clamp(radius ?? options.Value.DefaultRadius, 50, options.Value.MaxRadius);

    public async Task<NearbyResponse> FindAsync(double lat, double lon, int radius, CancellationToken ct)
    {
        var here = new Point(lon, lat) { SRID = 4326 };

        // Every heavy station right around, plus the nearest of each heavy mode even beyond the radius:
        // it is what most people look for.
        var candidates = await QueryStopsAsync(
            db.StopAreas.Where(s => HeroTypes.Contains(s.Type) && s.Location.IsWithinDistance(here, HeroLineDistance)), here, ct);
        foreach (var type in HeroTypes)
            candidates.AddRange(await QueryStopsAsync(
                db.StopAreas.Where(s => s.Type == type && s.Location.IsWithinDistance(here, HeroMaxDistance)), here, ct, limit: 1));
        // Picked by distance, shown grouped by mode so Métro lines are not split by an RER station.
        var heroes = ByMode(PickHeroes(candidates));
        var heroIds = heroes.Select(h => h.Id).ToHashSet();

        var stops = ByMode((await QueryStopsAsync(db.StopAreas.Where(s => s.Location.IsWithinDistance(here, radius)), here, ct))
            .Where(s => !heroIds.Contains(s.Id)));

        var stations = await db.VelibStations.AsNoTracking()
            .Where(s => s.Location.IsWithinDistance(here, radius))
            .OrderBy(s => s.Location.Distance(here))
            .Select(s => new { s.Id, s.Name, s.Location, Distance = (int)s.Location.Distance(here) })
            .ToListAsync(ct);

        var status = await GetVelibStatusAsync(ct);
        var velib = stations.Select(s => status.TryGetValue(s.Id, out var st)
                ? new VelibStationDto(s.Id, s.Name, s.Location.Y, s.Location.X, s.Distance, st.BikesOfType("mechanical"), st.BikesOfType("ebike"), st.DocksAvailable, st.IsRenting == 1)
                : new VelibStationDto(s.Id, s.Name, s.Location.Y, s.Location.X, s.Distance, 0, 0, 0, false))
            .ToList();

        return new NearbyResponse(radius, heroes, stops, velib);
    }

    /// <summary>Stops ordered by distance, with their lines in signage order.</summary>
    static async Task<List<StopAreaDto>> QueryStopsAsync(IQueryable<StopArea> query, Point here, CancellationToken ct, int? limit = null)
    {
        var ordered = query.AsNoTracking().OrderBy(s => s.Location.Distance(here));
        // Coordinates are read from the materialized Point: ST_X/ST_Y do not exist for geography columns.
        return (await (limit is { } n ? ordered.Take(n) : ordered)
            .Select(s => new { s.Id, s.Name, s.Type, s.Location, Distance = (int)s.Location.Distance(here), s.Lines })
            .ToListAsync(ct))
        .Select(s => new StopAreaDto(
            s.Id, s.Name, s.Type, s.Location.Y, s.Location.X, s.Distance,
            ModeOrder.Sort(s.Lines.Select(l => new LineDto(l.Id, l.Name, l.Mode, l.Color, l.TextColor, l.HasRealtime)))))
        .ToList();
    }

    async Task<Dictionary<long, GbfsStationStatus>> GetVelibStatusAsync(CancellationToken ct)
    {
        try
        {
            return await velibStatus.GetAllAsync(ct);
        }
        catch (HttpRequestException e)
        {
            // Transit info is still useful without bikes; stations show as unavailable.
            logger.LogWarning(e, "Vélib status feed unavailable");
            return [];
        }
    }
}
