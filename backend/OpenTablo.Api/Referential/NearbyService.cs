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
    public int ClampRadius(int? radius) => Math.Clamp(radius ?? options.Value.DefaultRadius, 50, options.Value.MaxRadius);

    public async Task<NearbyResponse> FindAsync(double lat, double lon, int radius, CancellationToken ct)
    {
        var here = new Point(lon, lat) { SRID = 4326 };

        // Coordinates are read from the materialized Point: ST_X/ST_Y do not exist for geography columns.
        var stops = (await db.StopAreas.AsNoTracking()
                .Where(s => s.Location.IsWithinDistance(here, radius))
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

        return new NearbyResponse(radius, stops, velib);
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
