using OpenTablo.Api.Contracts;
using OpenTablo.Api.Referential;

namespace OpenTablo.Api.Tests;

public class StationSearchTests
{
    static LineDto Line(string id, string name, string mode) => new(id, name, mode, "#000000", "#ffffff");

    [Theory]
    [InlineData("Glacière", "glaciere")]
    [InlineData("Saint-Michel Notre-Dame", "saint michel notre dame")]
    [InlineData("  Gare  d'Austerlitz ", "gare d austerlitz")]
    public void Normalizes_accents_case_and_punctuation(string name, string expected) =>
        Assert.Equal(expected, SearchText.Normalize(name));

    [Fact]
    public void Merges_the_modes_of_one_station_under_the_best_ranked_name()
    {
        var merged = StationSearchService.Merge([
            new("Gare de Lyon", "Paris 12e", 48.8458, 2.3737, [Line("C01384", "14", "metro"), Line("C01371", "1", "metro")]),
            new("Paris Gare de Lyon", "Paris 12e", 48.8443, 2.3740, [Line("C01742", "A", "rail"), Line("C01371", "1", "metro")]),
            new("Gare de Lyon", "Paris 12e", 48.8450, 2.3720, [Line("C01077", "20", "bus")]),
            new("Gare de Lyon", "Lyon", 45.7600, 4.8600, [Line("X", "C3", "bus")]),
        ], max: 5);

        Assert.Equal(2, merged.Count);
        Assert.Equal("Gare de Lyon", merged[0].Name);
        Assert.Equal(["1", "14", "A", "20"], merged[0].Lines.Select(l => l.Name));
        Assert.Equal("Lyon", merged[1].Town);
    }

    [Fact]
    public void Links_rail_lines_to_the_whole_stop_area()
    {
        var stopAreaOfStop = new Dictionary<string, string> { ["22083"] = "43153" };

        Assert.Equal("470195", ReferentialImporter.StopAreaOf("IDFM:monomodalStopPlace:470195", stopAreaOfStop));
        Assert.Equal("43153", ReferentialImporter.StopAreaOf("IDFM:22083", stopAreaOfStop));
        Assert.Null(ReferentialImporter.StopAreaOf("IDFM:999", stopAreaOfStop));
    }
}
