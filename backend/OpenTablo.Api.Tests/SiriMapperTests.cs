using System.Text.Json;
using OpenTablo.Api.Contracts;
using OpenTablo.Api.Prim;

namespace OpenTablo.Api.Tests;

public class SiriMapperTests
{
    static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    static IReadOnlyList<MonitoredStopVisit> LoadVisits()
    {
        var envelope = JsonSerializer.Deserialize<SiriEnvelope>(File.ReadAllText("Fixtures/stop-monitoring-bastille.json"), Web);
        return envelope!.Siri!.ServiceDelivery!.StopMonitoringDelivery![0].MonitoredStopVisit!;
    }

    [Fact]
    public void LineId_strips_the_SIRI_prefix() =>
        Assert.Equal("C01371", SiriMapper.LineId("STIF:Line::C01371:"));

    [Fact]
    public void Groups_real_payload_by_line_and_destination()
    {
        var visits = LoadVisits();
        var now = DateTimeOffset.Parse("2026-10-14T10:26:47Z");
        var metro1 = new LineDto("C01371", "1", "metro", "#ffcd00", "#000000");

        var groups = SiriMapper.ToDepartureGroups(visits, new Dictionary<string, LineDto> { ["C01371"] = metro1 }, now);

        Assert.NotEmpty(groups);
        Assert.Equal(groups.Count, groups.DistinctBy(g => (g.LineId, g.Destination)).Count());
        Assert.All(groups, g => Assert.InRange(g.Next.Count, 1, 3));
        Assert.All(groups, g => Assert.Equal(g.Next.OrderBy(d => d.Expected), g.Next));
        // Known lines sort first (metro before unknown-mode lines).
        Assert.Equal("C01371", groups[0].LineId);
        Assert.Contains(groups, g => g.Destination == "Château de Vincennes");
    }

    [Fact]
    public void Drops_departures_beyond_two_hours()
    {
        var now = DateTimeOffset.Parse("2026-10-14T10:26:47Z");
        var far = new MonitoredStopVisit(new MonitoredVehicleJourney(
            new SiriValue("STIF:Line::C01371:"), [new SiriValue("La Défense")],
            new MonitoredCall(false, null, null, now.AddHours(3), null, "onTime", null)));

        Assert.Empty(SiriMapper.ToDepartureGroups([far], new Dictionary<string, LineDto>(), now));
    }
}
