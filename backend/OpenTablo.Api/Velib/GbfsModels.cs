using System.Text.Json.Serialization;

namespace OpenTablo.Api.Velib;

public sealed record GbfsFeed<T>(GbfsStations<T> Data);

public sealed record GbfsStations<T>(IReadOnlyList<T> Stations);

public sealed record GbfsStationInformation(
    [property: JsonPropertyName("station_id")] long StationId,
    string Name,
    double Lat,
    double Lon,
    int Capacity);

public sealed record GbfsStationStatus(
    [property: JsonPropertyName("station_id")] long StationId,
    // Vélib publishes this as a list of single-entry objects: [{"mechanical":3},{"ebike":1}].
    [property: JsonPropertyName("num_bikes_available_types")] IReadOnlyList<Dictionary<string, int>>? BikeTypes,
    [property: JsonPropertyName("num_docks_available")] int DocksAvailable,
    [property: JsonPropertyName("is_renting")] int IsRenting);
