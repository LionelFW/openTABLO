using OpenTablo.Api.Contracts;
using OpenTablo.Api.Referential;

namespace OpenTablo.Api.Tests;

public class NearbyServiceTests
{
    static StopAreaDto Stop(string id, string type, int distance, params string[] lines) =>
        new(id, id, type, 0, 0, distance, lines.Select(l => new LineDto(l, l, type, "#000000", "#ffffff")).ToList());

    [Fact]
    public void Keeps_each_line_once_at_its_nearest_station()
    {
        var heroes = NearbyService.PickHeroes([
            Stop("Corvisart", "metro", 150, "M6"),
            Stop("Glaciere", "metro", 60, "M6"),
            Stop("Italie", "metro", 190, "M5", "M7", "M6"),
            Stop("CiteU", "rail", 2400, "RERB"),
            Stop("Glaciere", "metro", 60, "M6"), // also the nearest-metro fallback
        ]);

        Assert.Equal(["Glaciere", "Italie", "CiteU"], heroes.Select(h => h.Id));
    }

    [Fact]
    public void Groups_stops_by_mode_then_distance() =>
        Assert.Equal(["M1", "M2", "R", "T", "B"], NearbyService.ByMode([
            Stop("B", "bus", 10), Stop("M2", "metro", 300), Stop("R", "rail", 120), Stop("T", "tram", 50), Stop("M1", "metro", 140),
        ]).Select(s => s.Id));
}
