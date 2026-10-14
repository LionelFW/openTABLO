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

    public int ClampRadius(int? radius) => Math.Clamp(radius ?? options.Value.DefaultRadius, 50, options.Value.MaxRadius);

    public async Task<NearbyResponse> FindAsync(double lat, double lon, int radius, CancellationToken ct)
    {
        var here = new Point(lon, lat) { SRID = 4326 };

        // One hero per heavy mode, searched beyond the radius so the nearest station shows even with a small radius.
        var heroes = PickHeroes(await QueryStopsAsync(
            db.StopAreas.Where(s => HeroTypes.Contains(s.Type) && s.Location.IsWithinDistance(here, options.Value.HeroMaxDistance)), here, ct));
        var heroIds = heroes.Select(h => h.Id).ToHashSet();

        var stops = (await QueryStopsAsync(db.StopAreas.Where(s => s.Location.IsWithinDistance(here, radius)), here, ct))
            .Where(s => !heroIds.Contains(s.Id))
            .ToList();

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

    /// <summary>The nearest stop of each hero type, nearest first.</summary>
    public static List<StopAreaDto> PickHeroes(IEnumerable<StopAreaDto> stops) =>
        stops.Where(s => HeroTypes.Contains(s.Type))
            .GroupBy(s => s.Type)
            .Select(g => g.MinBy(s => s.Distance)!)
            .OrderBy(s => s.Distance)
            .ToList();

    /// <summary>Stops ordered by distance, with their lines in signage order.</summary>
    static async Task<List<StopAreaDto>> QueryStopsAsync(IQueryable<StopArea> query, Point here, CancellationToken ct) =>
        // Coordinates are read from the materialized Point: ST_X/ST_Y do not exist for geography columns.
        (await query.AsNoTracking()
            .OrderBy(s => s.Location.Distance(here))
            .Select(s => new { s.Id, s.Name, s.Type, s.Location, Distance = (int)s.Location.Distance(here), s.Lines })
            .ToListAsync(ct))
        .Select(s => new StopAreaDto(
            s.Id, s.Name, s.Type, s.Location.Y, s.Location.X, s.Distance,
            s.Lines
                .OrderBy(l => ModeOrder.Of(l.Mode)).ThenBy(l => l.Name.Length).ThenBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
                .Select(l => new LineDto(l.Id, l.Name, l.Mode, l.Color, l.TextColor))
                .ToList()))
        .ToList();

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
