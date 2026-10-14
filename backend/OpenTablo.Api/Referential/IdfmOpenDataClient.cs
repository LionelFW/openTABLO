namespace OpenTablo.Api.Referential;

/// <summary>Bulk exports from the IDFM open data portal. Free and unmetered, but heavy: only used by the importer.</summary>
public sealed class IdfmOpenDataClient(HttpClient http)
{
    public Task<List<IdfmLineRow>> GetLinesAsync(CancellationToken ct) => ExportAsync<IdfmLineRow>(IdfmLineRow.Dataset, IdfmLineRow.Fields, ct);

    public Task<List<IdfmStopRow>> GetStopsAsync(CancellationToken ct) => ExportAsync<IdfmStopRow>(IdfmStopRow.Dataset, IdfmStopRow.Fields, ct);

    public Task<List<IdfmStopLineRow>> GetStopLinesAsync(CancellationToken ct) => ExportAsync<IdfmStopLineRow>(IdfmStopLineRow.Dataset, IdfmStopLineRow.Fields, ct);

    async Task<List<T>> ExportAsync<T>(string dataset, string fields, CancellationToken ct) =>
        await http.GetFromJsonAsync<List<T>>($"{dataset}/exports/json?select={fields}", ct)
        ?? throw new InvalidOperationException($"Empty export for dataset {dataset}.");
}
