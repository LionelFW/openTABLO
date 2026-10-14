using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using OpenTablo.Api.Contracts;
using OpenTablo.Api.Data;
using OpenTablo.Api.Options;

namespace OpenTablo.Api.Prim;

public sealed class DeparturesService(PrimClient prim, TabloDbContext db, HybridCache cache, IOptions<PrimOptions> options, TimeProvider clock)
{
    public async Task<DeparturesResponse> GetAsync(string stopAreaId, CancellationToken ct)
    {
        var ttl = TimeSpan.FromSeconds(options.Value.DeparturesCacheSeconds);
        // HybridCache also merges concurrent requests for the same stop into one PRIM call.
        return await cache.GetOrCreateAsync(
            $"departures:{stopAreaId}",
            async token =>
            {
                var siri = await prim.GetStopMonitoringAsync(stopAreaId, token);
                var visits = siri?.Siri?.ServiceDelivery?.StopMonitoringDelivery?.FirstOrDefault()?.MonitoredStopVisit ?? [];
                var lineIds = visits.Select(v => SiriMapper.LineId(v.MonitoredVehicleJourney?.LineRef?.Value)).Distinct().ToList();
                var lines = await db.Lines.AsNoTracking()
                    .Where(l => lineIds.Contains(l.Id))
                    .ToDictionaryAsync(l => l.Id, l => new LineDto(l.Id, l.Name, l.Mode, l.Color, l.TextColor), token);
                var now = clock.GetUtcNow();
                return new DeparturesResponse(stopAreaId, now, SiriMapper.ToDepartureGroups(visits, lines, now));
            },
            new HybridCacheEntryOptions { Expiration = ttl, LocalCacheExpiration = ttl },
            cancellationToken: ct);
    }
}
