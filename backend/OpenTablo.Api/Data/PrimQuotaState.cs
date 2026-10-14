namespace OpenTablo.Api.Data;

/// <summary>Single row shared by all API instances so the PRIM limits hold cluster-wide.</summary>
public class PrimQuotaState
{
    public const int SingletonId = 1;

    public int Id { get; set; }
    public DateOnly Day { get; set; }
    public int Used { get; set; }
    public DateTimeOffset NextSlot { get; set; }
}
