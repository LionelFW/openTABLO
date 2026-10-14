namespace OpenTablo.Api.Velib;

/// <summary>Vélib Métropole GBFS open data feeds (no key, no quota).</summary>
public sealed class VelibClient(HttpClient http)
{
    public async Task<IReadOnlyList<GbfsStationInformation>> GetStationInformationAsync(CancellationToken ct) =>
        (await http.GetFromJsonAsync<GbfsFeed<GbfsStationInformation>>("station_information.json", ct))?.Data.Stations ?? [];

    public async Task<IReadOnlyList<GbfsStationStatus>> GetStationStatusAsync(CancellationToken ct) =>
        (await http.GetFromJsonAsync<GbfsFeed<GbfsStationStatus>>("station_status.json", ct))?.Data.Stations ?? [];
}
