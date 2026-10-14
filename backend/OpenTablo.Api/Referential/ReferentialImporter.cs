using System.Text.Json;
using System.Text.Json.Serialization;
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
        var zdcOfZda = (await idfm.GetStopZonesAsync(ct)).Where(r => r.ZdcId is not null).DistinctBy(r => r.ZdaId).ToDictionary(r => r.ZdaId, r => r.ZdcId!);

        var realtime = RealtimeLines();
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
                HasRealtime = realtime?.Contains(r.IdLine) ?? true,
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
                Town = g.Select(r => r.Town).FirstOrDefault(t => t is not null),
                SearchName = SearchText.Normalize(g.First().Name ?? g.Key),
                ZdcId = zdcOfZda.GetValueOrDefault(g.Key),
            });

        var stopAreaOfStop = stopRows.Where(r => r.ZdaId is not null).ToDictionary(r => r.ArrId, r => r.ZdaId!);
        foreach (var (zdaId, lineId) in stopLineRows
            .Select(r => (Zda: StopAreaOf(r.StopId, stopAreaOfStop), Line: StripPrefix(r.LineId)))
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

    /// <summary>
    /// Lines PRIM has real-time data for. realtime-lines.json is a manual export, made while logged in to the IDFM portal:
    /// datasets/perimetre-des-donnees-tr-disponibles-plateforme-idfm/exports/json?select=line,name_line,count(*) as stops&amp;group_by=line,name_line&amp;order_by=line
    /// Null when the file is absent, so that no line is marked as lacking real time.
    /// </summary>
    static HashSet<string>? RealtimeLines()
    {
        using var stream = typeof(ReferentialImporter).Assembly.GetManifestResourceStream("realtime-lines.json");
        if (stream is null) return null;
        var rows = JsonSerializer.Deserialize<List<RealtimeLineRow>>(stream) ?? [];
        return rows.Select(r => r.Line.Split(':', StringSplitOptions.RemoveEmptyEntries)[^1]).ToHashSet(); // "STIF:Line::C01371:"
    }

    sealed record RealtimeLineRow([property: JsonPropertyName("line")] string Line);

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

    const string StopAreaPrefix = "monomodalStopPlace:";

    /// <summary>Rail lines are linked to the whole stop area, other modes to each stop of it.</summary>
    public static string? StopAreaOf(string stopId, IReadOnlyDictionary<string, string> stopAreaOfStop)
    {
        var id = StripPrefix(stopId);
        return id.StartsWith(StopAreaPrefix, StringComparison.Ordinal) ? id[StopAreaPrefix.Length..] : stopAreaOfStop.GetValueOrDefault(id);
    }

    static string HexColor(string? hex, string fallback) => hex is { Length: 6 } ? "#" + hex.ToLowerInvariant() : fallback;
}
