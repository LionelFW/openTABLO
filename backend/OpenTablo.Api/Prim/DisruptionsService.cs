using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using OpenTablo.Api.Contracts;
using OpenTablo.Api.Options;

namespace OpenTablo.Api.Prim;

public sealed class DisruptionsService(PrimClient prim, HybridCache cache, IOptions<PrimOptions> options, TimeProvider clock)
{
    static readonly TimeZoneInfo Paris = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");

    public async Task<IReadOnlyList<LineDisruptionsDto>> GetForLinesAsync(IReadOnlyCollection<string> lineIds, CancellationToken ct)
    {
        var ttl = TimeSpan.FromSeconds(options.Value.DisruptionsCacheSeconds);
        // A single bulk call covers the whole region, so every user and stop shares it.
        var all = await cache.GetOrCreateAsync(
            "disruptions:active",
            async token =>
            {
                var bulk = await prim.GetDisruptionsAsync(token);
                var parisNow = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Paris).DateTime;
                return bulk is null ? new Dictionary<string, List<DisruptionDto>>() : DisruptionMapper.ActiveByLine(bulk, parisNow);
            },
            new HybridCacheEntryOptions { Expiration = ttl, LocalCacheExpiration = ttl },
            cancellationToken: ct);

        return lineIds
            .Where(all.ContainsKey)
            .Select(id => new LineDisruptionsDto(id, all[id]))
            .ToList();
    }
}
