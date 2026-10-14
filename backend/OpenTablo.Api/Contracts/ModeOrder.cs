namespace OpenTablo.Api.Contracts;

/// <summary>Display order used by RATP signage: heavy modes first.</summary>
public static class ModeOrder
{
    public static int Of(string? mode) => mode switch
    {
        "metro" => 0,
        "rail" => 1,
        "tram" => 2,
        "funicular" or "cableway" => 3,
        _ => 4,
    };
}
