using NetTopologySuite.Geometries;

namespace OpenTablo.Api.Data;

/// <summary>An IDFM "zone d'arrêt": the platforms of one mode grouped under one name.</summary>
public class StopArea
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string Type { get; set; }
    public required Point Location { get; set; }
    /// <summary>Commune or arrondissement, e.g. "Paris 13e"; tells apart stops with the same name.</summary>
    public string? Town { get; set; }
    /// <summary><see cref="Name"/> normalized by <see cref="Referential.SearchText"/>, for station search.</summary>
    public string SearchName { get; set; } = "";
    public List<TransitLine> Lines { get; set; } = [];
}
