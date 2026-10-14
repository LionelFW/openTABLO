using OpenTablo.Api.Contracts;
using OpenTablo.Api.Referential;

namespace OpenTablo.Api.Tests;

public class NearbyServiceTests
{
    static StopAreaDto Stop(string id, string type, int distance) => new(id, id, type, 48.85, 2.35, distance, []);

    [Fact]
    public void Picks_the_nearest_metro_and_rail_station_nearest_first()
    {
        var heroes = NearbyService.PickHeroes([
            Stop("bus", "bus", 10),
            Stop("metro-far", "metro", 300),
            Stop("rail", "rail", 200),
            Stop("metro-near", "metro", 50),
            Stop("tram", "tram", 20),
        ]);

        Assert.Equal(["metro-near", "rail"], heroes.Select(h => h.Id));
    }

    [Fact]
    public void Returns_no_hero_without_metro_or_rail()
    {
        Assert.Empty(NearbyService.PickHeroes([Stop("bus", "bus", 10), Stop("tram", "tram", 20)]));
    }
}
