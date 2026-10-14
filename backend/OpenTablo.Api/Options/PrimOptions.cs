using System.ComponentModel.DataAnnotations;

namespace OpenTablo.Api.Options;

public sealed class PrimOptions
{
    public const string Section = "Prim";

    [Required]
    public string ApiKey { get; set; } = string.Empty;

    [Required, Url]
    public string BaseUrl { get; set; } = "https://prim.iledefrance-mobilites.fr/marketplace/";

    /// <summary>Calls allowed per Paris calendar day, shared by every API instance.</summary>
    [Range(1, 1_000_000)]
    public int DailyBudget { get; set; } = 900;

    /// <summary>Calls allowed per second, shared by every API instance.</summary>
    [Range(1, 100)]
    public int MaxPerSecond { get; set; } = 4;

    [Range(5, 3600)]
    public int DeparturesCacheSeconds { get; set; } = 45;

    [Range(30, 3600)]
    public int DisruptionsCacheSeconds { get; set; } = 300;
}
