using System.Text.Json;
using OpenTablo.Api.Prim;

namespace OpenTablo.Api.Tests;

public class ScheduleServiceTests
{
    // Trimmed from a real stop_schedules answer at Cimetière de Montrouge (stop_area:IDFM:70621).
    const string Sample = """
        {"stop_schedules":[
          {"route":{"line":{"id":"line:IDFM:C01104"}},"date_times":[],"additional_informations":"no_departure_this_day"},
          {"route":{"line":{"id":"line:IDFM:C01104"}},"date_times":[{"date_time":"20261014T182200"}]},
          {"route":{"line":{"id":"line:IDFM:C01155"}},"date_times":[{"date_time":"20261015T054000"}]},
          {"route":{"line":{"id":"line:IDFM:C01155"}},"date_times":[{"date_time":"20261014T181900"}]}
        ]}
        """;

    [Fact]
    public void Merges_routes_per_line_earliest_first()
    {
        var byLine = ScheduleService.NextByLine(JsonSerializer.Deserialize<StopSchedulesResponse>(Sample));

        Assert.Equal(["C01104", "C01155"], byLine.Select(l => l.LineId));
        Assert.Equal(new DateTimeOffset(2026, 10, 14, 18, 19, 0, TimeSpan.FromHours(2)), byLine[1].Planned[0]);
        Assert.Equal(new DateTimeOffset(2026, 10, 15, 5, 40, 0, TimeSpan.FromHours(2)), byLine[1].Planned[1]);
    }

    [Fact]
    public void Reads_paris_local_time_across_the_dst_change() =>
        Assert.Equal(TimeSpan.FromHours(1), ScheduleService.ParseParis("20261026T003000")!.Value.Offset);
}
