namespace OpenTablo.Api.Data;

public class TransitLine
{
    /// <summary>IDFM line code, e.g. C01371.</summary>
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string Mode { get; set; }
    public required string Color { get; set; }
    public required string TextColor { get; set; }
    /// <summary>False when PRIM publishes no real-time departures for this line at all.</summary>
    public bool HasRealtime { get; set; } = true;
    public List<StopArea> StopAreas { get; set; } = [];
}
