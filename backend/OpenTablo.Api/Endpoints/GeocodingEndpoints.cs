using OpenTablo.Api.Contracts;
using OpenTablo.Api.Geocoding;

namespace OpenTablo.Api.Endpoints;

public static class GeocodingEndpoints
{
    const int MinQueryLength = 3;
    const int MaxQueryLength = 200;

    public static RouteGroupBuilder MapGeocodingEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/geocode", SearchAsync);
        return api;
    }

    static async Task<IReadOnlyList<AddressDto>> SearchAsync(string q, GeocodingClient geocoder, CancellationToken ct)
    {
        var query = q.Trim();
        if (query.Length < MinQueryLength) return [];
        return await geocoder.SearchAsync(query[..Math.Min(query.Length, MaxQueryLength)], ct);
    }
}
