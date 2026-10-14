using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenTablo.Api.Data;
using OpenTablo.Api.Options;

namespace OpenTablo.Api.Referential;

/// <summary>Keeps the stored referentials fresh: imports at startup when stale, then checks hourly.</summary>
public sealed class ReferentialRefreshService(
    IServiceScopeFactory scopes, IOptions<OpenDataOptions> options, TimeProvider clock, ILogger<ReferentialRefreshService> logger)
    : BackgroundService
{
    static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CheckInterval, clock);
        do
        {
            await RefreshIfStaleAsync(ReferentialImporter.TransitReferential, (i, ct) => i.ImportTransitAsync(ct), stoppingToken);
            await RefreshIfStaleAsync(ReferentialImporter.VelibReferential, (i, ct) => i.ImportVelibAsync(ct), stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    async Task RefreshIfStaleAsync(string referential, Func<ReferentialImporter, CancellationToken, Task> import, CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<TabloDbContext>();
            var last = await db.ReferentialImports.AsNoTracking().Where(r => r.Name == referential).Select(r => (DateTimeOffset?)r.ImportedAt).SingleOrDefaultAsync(ct);
            if (last is not null && clock.GetUtcNow() - last < options.Value.ReferentialMaxAge) return;

            logger.LogInformation("Refreshing {Referential} (last import: {Last})", referential, last);
            await import(scope.ServiceProvider.GetRequiredService<ReferentialImporter>(), ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // Keep serving the previous data; the next tick retries.
            logger.LogError(e, "Refresh of {Referential} failed", referential);
        }
    }
}
