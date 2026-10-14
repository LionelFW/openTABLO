using System.Globalization;
using OpenTablo.Api.Contracts;

namespace OpenTablo.Api.Geocoding;

/// <summary>
/// French national address search (Géoplateforme). Called server-side so the user's
/// device never contacts a third party with their address query.
/// </summary>
public sealed class GeocodingClient(HttpClient http)
{
    // Biases ranking towards Île-de-France without excluding other results.
    const double BiasLat = 48.8566, BiasLon = 2.3522;
    const int MaxResults = 5;

    public async Task<IReadOnlyList<AddressDto>> SearchAsync(string query, CancellationToken ct)
    {
        var url = string.Create(CultureInfo.InvariantCulture,
            $"search?limit={MaxResults}&lat={BiasLat}&lon={BiasLon}&q={Uri.EscapeDataString(query)}");
        var response = await http.GetFromJsonAsync<GeocoderResponse>(url, ct);
        return (response?.Features ?? [])
            .Where(f => f.Geometry.Coordinates.Count >= 2)
            .Select(f => new AddressDto(f.Properties.Label, f.Geometry.Coordinates[1], f.Geometry.Coordinates[0]))
            .ToList();
    }
}
