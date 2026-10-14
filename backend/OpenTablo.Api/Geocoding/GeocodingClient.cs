using System.Globalization;
using OpenTablo.Api.Contracts;

namespace OpenTablo.Api.Geocoding;

/// <summary>
/// French national address search (Géoplateforme). Called server-side so the user's
/// device never contacts a third party with their address query.
/// </summary>
public sealed class GeocodingClient(HttpClient http)
{
    // Biases ranking towards Île-de-France; the department filter below does the excluding.
    const double BiasLat = 48.8566, BiasLon = 2.3522;
    const int MaxResults = 5;
    // The geocoder cannot filter on several departments, so ask for more and filter here.
    const int FetchedResults = 30;
    static readonly HashSet<string> ServedDepartments = ["75", "92", "93", "94", "91", "95", "78", "77", "60"];

    public async Task<IReadOnlyList<AddressDto>> SearchAsync(string query, CancellationToken ct)
    {
        var url = string.Create(CultureInfo.InvariantCulture,
            $"search?limit={FetchedResults}&lat={BiasLat}&lon={BiasLon}&q={Uri.EscapeDataString(query)}");
        return ToAddresses(await http.GetFromJsonAsync<GeocoderResponse>(url, ct));
    }

    /// <summary>The first results located in a served department.</summary>
    public static List<AddressDto> ToAddresses(GeocoderResponse? response) =>
        (response?.Features ?? [])
            .Where(f => f.Geometry.Coordinates.Count >= 2 && f.Properties.CityCode is { Length: >= 2 } code && ServedDepartments.Contains(code[..2]))
            .Take(MaxResults)
            .Select(f => new AddressDto(f.Properties.Label, f.Geometry.Coordinates[1], f.Geometry.Coordinates[0]))
            .ToList();
}
