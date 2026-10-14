using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using OpenTablo.Api.Data;
using OpenTablo.Api.Velib;

namespace OpenTablo.Api.Referential;

/// <summary>Replaces the stored stop, line and Vélib station referentials with fresh open data.</summary>
public sealed class ReferentialImporter(
    TabloDbContext db, IdfmOpenDataClient idfm, VelibClient velib, TimeProvider clock, ILogger<ReferentialImporter> logger)
{
    public const string TransitReferential = "idfm-transit";
    public const string VelibReferential = "velib-stations";
    const long ImportLockKey = 0x7AB10;
    const string DefaultColor = "#0a0082";

    public async Task ImportTransitAsync(CancellationToken ct)
    {
        var lineRows = await idfm.GetLinesAsync(ct);
        var stopRows = await idfm.GetStopsAsync(ct);
        var stopLineRows = await idfm.GetStopLinesAsync(ct);

        var lines = lineRows
            .GroupBy(r => r.IdLine)
            .Select(g => g.First())
            .ToDictionary(r => r.IdLine, r => new TransitLine
            {
                Id = r.IdLine,
                Name = r.ShortName ?? r.IdLine,
                Mode = r.TransportMode ?? "bus",
                Color = HexColor(r.Colour, DefaultColor),
                TextColor = HexColor(r.TextColour, "#ffffff"),
            });

        var stopAreas = stopRows
            .Where(r => r.ZdaId is not null && r.GeoPoint is not null)
            .GroupBy(r => r.ZdaId!)
            .ToDictionary(g => g.Key, g => new StopArea
            {
                Id = g.Key,
                Name = g.First().Name ?? g.Key,
                Type = g.First().Type ?? "bus",
                Location = Point(g.Average(r => r.GeoPoint!.Lat), g.Average(r => r.GeoPoint!.Lon)),
            });

        var stopAreaOfStop = stopRows.Where(r => r.ZdaId is not null).ToDictionary(r => r.ArrId, r => r.ZdaId!);
        foreach (var (zdaId, lineId) in stopLineRows
            .Select(r => (Zda: stopAreaOfStop.GetValueOrDefault(StripPrefix(r.StopId)), Line: StripPrefix(r.LineId)))
            .Where(x => x.Zda is not null)
            .Distinct())
        {
            if (stopAreas.TryGetValue(zdaId!, out var area) && lines.TryGetValue(lineId, out var line))
                area.Lines.Add(line);
        }

        await ReplaceAsync(TransitReferential, async () =>
        {
            await db.StopAreas.ExecuteDeleteAsync(ct);
            await db.Lines.ExecuteDeleteAsync(ct);
            db.Lines.AddRange(lines.Values);
            db.StopAreas.AddRange(stopAreas.Values);
        }, ct);
        logger.LogInformation("Imported {StopAreas} stop areas and {Lines} lines", stopAreas.Count, lines.Count);
    }

    public async Task ImportVelibAsync(CancellationToken ct)
    {
        var stations = (await velib.GetStationInformationAsync(ct))
            .GroupBy(s => s.StationId)
            .Select(g => g.First())
            .Select(s => new VelibStation { Id = s.StationId, Name = s.Name, Capacity = s.Capacity, Location = Point(s.Lat, s.Lon) })
            .ToList();

        await ReplaceAsync(VelibReferential, async () =>
        {
            await db.VelibStations.ExecuteDeleteAsync(ct);
            db.VelibStations.AddRange(stations);
        }, ct);
        logger.LogInformation("Imported {Stations} Vélib stations", stations.Count);
    }

    /// <summary>Swaps data in one transaction so readers never see a half-empty table.</summary>
    async Task ReplaceAsync(string referential, Func<Task> replace, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var locked = await db.Database.SqlQuery<bool>($"""SELECT pg_try_advisory_xact_lock({ImportLockKey}) AS "Value" """).ToListAsync(ct);
        if (!locked[0])
        {
            logger.LogInformation("Another instance is importing {Referential}; skipping", referential);
            return;
        }

        await replace();
        var import = await db.ReferentialImports.FindAsync([referential], ct);
        if (import is null) db.ReferentialImports.Add(import = new ReferentialImport { Name = referential });
        import.ImportedAt = clock.GetUtcNow();

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();
    }

    static Point Point(double lat, double lon) => new(lon, lat) { SRID = 4326 };

    static string StripPrefix(string id) => id.Replace("IDFM:", string.Empty);

    static string HexColor(string? hex, string fallback) => hex is { Length: 6 } ? "#" + hex.ToLowerInvariant() : fallback;
}
