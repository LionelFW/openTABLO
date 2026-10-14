using NetTopologySuite.Geometries;

namespace OpenTablo.Api.Data;

/// <summary>An IDFM "zone d'arrêt": the platforms of one mode grouped under one name.</summary>
public class StopArea
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string Type { get; set; }
    public required Point Location { get; set; }
    public List<TransitLine> Lines { get; set; } = [];
}
