using Microsoft.AspNetCore.Http.HttpResults;
using OpenTablo.Api.Contracts;
using OpenTablo.Api.Prim;
using OpenTablo.Api.Referential;

namespace OpenTablo.Api.Endpoints;

public static class TransitEndpoints
{
    const int MaxDisruptionLines = 200;

    public static RouteGroupBuilder MapTransitEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/nearby", GetNearbyAsync);
        api.MapGet("/stations", (string q, StationSearchService search, CancellationToken ct) => search.SearchAsync(q, ct));
        api.MapGet("/departures/{stopAreaId}", GetDeparturesAsync);
        api.MapGet("/disruptions", GetDisruptionsAsync);
        api.MapGet("/usage", (PrimQuota quota, CancellationToken ct) => quota.GetUsageAsync(ct));
        return api;
    }

    // Coordinates are only used to compute this response: they are never stored or logged.
    static async Task<Results<Ok<NearbyResponse>, ValidationProblem>> GetNearbyAsync(
        double lat, double lon, int? radius, NearbyService nearby, CancellationToken ct)
    {
        if (lat is < -90 or > 90 || lon is < -180 or > 180)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["coordinates"] = ["Latitude or longitude out of range."] });

        return TypedResults.Ok(await nearby.FindAsync(lat, lon, nearby.ClampRadius(radius), ct));
    }

    static async Task<Results<Ok<DeparturesResponse>, ValidationProblem>> GetDeparturesAsync(
        string stopAreaId, DeparturesService departures, CancellationToken ct)
    {
        if (stopAreaId.Length is 0 or > 12 || !stopAreaId.All(char.IsAsciiDigit))
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { [nameof(stopAreaId)] = ["Expected a numeric IDFM stop area id."] });

        return TypedResults.Ok(await departures.GetAsync(stopAreaId, ct));
    }

    static async Task<Results<Ok<IReadOnlyList<LineDisruptionsDto>>, ValidationProblem>> GetDisruptionsAsync(
        string lines, DisruptionsService disruptions, CancellationToken ct)
    {
        var ids = lines.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();
        if (ids.Count > MaxDisruptionLines)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { [nameof(lines)] = [$"At most {MaxDisruptionLines} lines per request."] });

        return TypedResults.Ok(await disruptions.GetForLinesAsync(ids, ct));
    }
}
