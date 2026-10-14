using System.Text.Json.Serialization;

namespace OpenTablo.Api.Referential;

// Rows of the IDFM open data exports (exports/json), limited to the selected columns.

public sealed record IdfmLineRow(
    [property: JsonPropertyName("id_line")] string IdLine,
    [property: JsonPropertyName("shortname_line")] string? ShortName,
    [property: JsonPropertyName("transportmode")] string? TransportMode,
    [property: JsonPropertyName("colourweb_hexa")] string? Colour,
    [property: JsonPropertyName("textcolourweb_hexa")] string? TextColour)
{
    public const string Dataset = "referentiel-des-lignes";
    public const string Fields = "id_line,shortname_line,transportmode,colourweb_hexa,textcolourweb_hexa";
}

public sealed record IdfmStopRow(
    [property: JsonPropertyName("arrid")] string ArrId,
    [property: JsonPropertyName("zdaid")] string? ZdaId,
    [property: JsonPropertyName("arrname")] string? Name,
    [property: JsonPropertyName("arrtype")] string? Type,
    [property: JsonPropertyName("arrgeopoint")] IdfmGeoPoint? GeoPoint,
    [property: JsonPropertyName("arrtown")] string? Town = null)
{
    public const string Dataset = "arrets";
    public const string Fields = "arrid,zdaid,arrname,arrtype,arrgeopoint,arrtown";
}

/// <summary>Links a stop area to its interchange zone ("zone de correspondance"), the stop area id Navitia uses.</summary>
public sealed record IdfmStopZoneRow(
    [property: JsonPropertyName("zdaid")] string ZdaId,
    [property: JsonPropertyName("zdcid")] string? ZdcId)
{
    public const string Dataset = "zones-d-arrets";
    public const string Fields = "zdaid,zdcid";
}

public sealed record IdfmGeoPoint(double Lat, double Lon);

/// <summary>Links a line (IDFM:{id_line}) to a stop (IDFM:{arrid}), or to a whole stop area (IDFM:monomodalStopPlace:{zdaid}) for rail.</summary>
public sealed record IdfmStopLineRow(
    [property: JsonPropertyName("id")] string LineId,
    [property: JsonPropertyName("stop_id")] string StopId)
{
    public const string Dataset = "arrets-lignes";
    public const string Fields = "id,stop_id";
}
