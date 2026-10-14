using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenTablo.Api.Contracts;
using OpenTablo.Api.Data;
using OpenTablo.Api.Options;

namespace OpenTablo.Api.Prim;

/// <summary>
/// Reserves PRIM call slots in PostgreSQL so the daily budget and per-second cap
/// hold across every API instance and survive restarts.
/// </summary>
public sealed class PrimQuota(IDbContextFactory<TabloDbContext> dbFactory, IOptions<PrimOptions> options, TimeProvider clock)
{
    static readonly TimeZoneInfo Paris = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");

    public async Task AcquireAsync(CancellationToken ct)
    {
        var o = options.Value;
        var today = ParisToday();
        var spacing = TimeSpan.FromSeconds(1.0 / o.MaxPerSecond);

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        // One atomic statement: count the call (resetting on a new day) and book the next free slot.
        var slots = await db.Database.SqlQuery<DateTimeOffset>($"""
            UPDATE "PrimQuota"
            SET "Used" = CASE WHEN "Day" = {today} THEN "Used" + 1 ELSE 1 END,
                "Day" = {today},
                "NextSlot" = GREATEST(now(), "NextSlot" + {spacing})
            WHERE "Id" = {PrimQuotaState.SingletonId} AND ("Day" <> {today} OR "Used" < {o.DailyBudget})
            RETURNING "NextSlot" AS "Value"
            """).ToListAsync(ct);

        if (slots.Count == 0) throw new QuotaExceededException($"Daily PRIM budget of {o.DailyBudget} calls reached.");

        var wait = slots[0] - clock.GetUtcNow();
        if (wait > TimeSpan.Zero) await Task.Delay(wait, clock, ct);
    }

    public async Task<PrimUsageDto> GetUsageAsync(CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var state = await db.PrimQuota.AsNoTracking().SingleAsync(q => q.Id == PrimQuotaState.SingletonId, ct);
        var used = state.Day == ParisToday() ? state.Used : 0;
        return new PrimUsageDto(ParisToday(), used, options.Value.DailyBudget);
    }

    DateOnly ParisToday() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Paris).DateTime);
}
