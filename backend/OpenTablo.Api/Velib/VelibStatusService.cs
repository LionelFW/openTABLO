using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using OpenTablo.Api.Options;

namespace OpenTablo.Api.Velib;

/// <summary>Live availability for all stations, fetched at most once per cache period.</summary>
public sealed class VelibStatusService(VelibClient velib, HybridCache cache, IOptions<OpenDataOptions> options)
{
    public async Task<Dictionary<long, GbfsStationStatus>> GetAllAsync(CancellationToken ct)
    {
        var ttl = TimeSpan.FromSeconds(options.Value.VelibStatusCacheSeconds);
        return await cache.GetOrCreateAsync(
            "velib:status",
            async token => (await velib.GetStationStatusAsync(token)).DistinctBy(s => s.StationId).ToDictionary(s => s.StationId),
            new HybridCacheEntryOptions { Expiration = ttl, LocalCacheExpiration = ttl },
            cancellationToken: ct);
    }
}
