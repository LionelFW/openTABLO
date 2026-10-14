using System.Text.Json;
using OpenTablo.Api.Prim;

namespace OpenTablo.Api.Tests;

public class DisruptionMapperTests
{
    static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    static DisruptionsBulk Load() =>
        JsonSerializer.Deserialize<DisruptionsBulk>(File.ReadAllText("Fixtures/disruptions-sample.json"), Web)!;

    [Fact]
    public void Keeps_only_disruptions_active_now_keyed_by_line_code()
    {
        var bulk = Load();
        var now = new DateTime(2026, 10, 14, 12, 0, 0);

        var active = DisruptionMapper.ActiveByLine(bulk, now);

        Assert.All(active.Keys, k => Assert.DoesNotContain("line:IDFM:", k));
        var all = active.Values.SelectMany(d => d).ToList();
        Assert.NotEmpty(all);
        // e1ecb78b starts on 2026-10-19, so it must not be reported on the 14th.
        Assert.DoesNotContain(all, d => d.Id.StartsWith("e1ecb78b"));
        Assert.Contains(all, d => d.Id.StartsWith("95148b0b"));
    }

    [Fact]
    public void Returns_nothing_when_every_period_is_over()
    {
        var bulk = new DisruptionsBulk(
            [new Disruption("x", [new ApplicationPeriod("20200101T000000", "20200102T000000")], "TRAVAUX", "BLOQUANTE", "t", null, null)],
            [new DisruptedLine("line:IDFM:C01371", [new ImpactedObject("line", "line:IDFM:C01371", ["x"])])]);

        Assert.Empty(DisruptionMapper.ActiveByLine(bulk, new DateTime(2026, 10, 14)));
    }
}
