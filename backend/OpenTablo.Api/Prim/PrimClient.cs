namespace OpenTablo.Api.Prim;

/// <summary>Raw PRIM calls. Every call is gated by <see cref="PrimQuota"/>; callers are expected to cache results.</summary>
public sealed class PrimClient(HttpClient http, PrimQuota quota, ILogger<PrimClient> logger)
{
    public Task<SiriEnvelope?> GetStopMonitoringAsync(string stopAreaId, CancellationToken ct) =>
        GetAsync<SiriEnvelope>($"stop-monitoring?MonitoringRef=STIF:StopArea:SP:{stopAreaId}:", ct);

    public Task<DisruptionsBulk?> GetDisruptionsAsync(CancellationToken ct) =>
        GetAsync<DisruptionsBulk>("disruptions_bulk/disruptions/v2", ct);

    async Task<T?> GetAsync<T>(string pathAndQuery, CancellationToken ct)
    {
        await quota.AcquireAsync(ct);
        using var response = await http.GetAsync(pathAndQuery, ct);
        logger.LogInformation("PRIM {Path} responded {Status}", pathAndQuery.Split('?')[0], (int)response.StatusCode);

        if ((int)response.StatusCode == StatusCodes.Status429TooManyRequests)
            throw new QuotaExceededException("PRIM rejected the call (429).");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(ct);
    }
}
