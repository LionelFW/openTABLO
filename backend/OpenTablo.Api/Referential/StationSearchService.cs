using Microsoft.EntityFrameworkCore;
using OpenTablo.Api.Contracts;
using OpenTablo.Api.Data;

namespace OpenTablo.Api.Referential;

/// <summary>Finds stations by name. Reads only the stored referential: no PRIM quota.</summary>
public sealed class StationSearchService(TabloDbContext db)
{
    const int MinQueryLength = 2;
    const int MaxQueryLength = 100;
    const int Candidates = 200;
    const int MaxResults = 5;
    // Métro, RER and bus stop areas of one station are separate in IDFM data, a few hundred metres apart at most.
    const double SameStationMeters = 400;

    public async Task<IReadOnlyList<StationDto>> SearchAsync(string query, CancellationToken ct)
    {
        var q = SearchText.Normalize(query.Length > MaxQueryLength ? query[..MaxQueryLength] : query);
        if (q.Length < MinQueryLength) return [];

        var candidates = await db.StopAreas.AsNoTracking()
            .Where(s => s.SearchName.Contains(q))
            // Heavy modes and names starting with the query come first, so they name the merged station.
            .OrderBy(s => s.Type == "metro" ? 0 : s.Type == "rail" ? 1 : s.Type == "tram" ? 2 : 3)
            .ThenBy(s => s.SearchName.StartsWith(q) ? 0 : 1)
            .ThenBy(s => s.Name.Length)
            .Take(Candidates)
            .Select(s => new { s.Name, s.Town, s.Location, s.Lines })
            .ToListAsync(ct);

        return Merge(candidates.Select(c => new StationDto(c.Name, c.Town, c.Location.Y, c.Location.X,
            c.Lines.Select(l => new LineDto(l.Id, l.Name, l.Mode, l.Color, l.TextColor)).ToList())), MaxResults);
    }

    /// <summary>Merges ranked stop areas lying close to a better-ranked one into it, keeping its name and position.</summary>
    public static List<StationDto> Merge(IEnumerable<StationDto> ranked, int max)
    {
        var stations = new List<(StationDto Anchor, List<LineDto> Lines)>();
        foreach (var stop in ranked)
        {
            var station = stations.FindIndex(s => DistanceMeters(s.Anchor, stop) <= SameStationMeters);
            if (station < 0) stations.Add((stop, [.. stop.Lines]));
            else stations[station].Lines.AddRange(stop.Lines);
        }
        return stations.Take(max)
            .Select(s => s.Anchor with { Lines = ModeOrder.Sort(s.Lines.DistinctBy(l => l.Id)) })
            .ToList();
    }

    // Equirectangular approximation: exact enough at a few hundred metres.
    static double DistanceMeters(StationDto a, StationDto b)
    {
        const double EarthRadius = 6_371_000, Rad = Math.PI / 180;
        var x = (b.Lon - a.Lon) * Rad * Math.Cos((a.Lat + b.Lat) / 2 * Rad);
        var y = (b.Lat - a.Lat) * Rad;
        return EarthRadius * Math.Sqrt(x * x + y * y);
    }
}
