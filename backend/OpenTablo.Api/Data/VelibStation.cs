using NetTopologySuite.Geometries;

namespace OpenTablo.Api.Data;

public class VelibStation
{
    public long Id { get; set; }
    public required string Name { get; set; }
    public required Point Location { get; set; }
    public int Capacity { get; set; }
}
