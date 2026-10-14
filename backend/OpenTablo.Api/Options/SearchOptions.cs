using System.ComponentModel.DataAnnotations;

namespace OpenTablo.Api.Options;

public sealed class SearchOptions
{
    public const string Section = "Search";

    [Range(50, 5000)]
    public int DefaultRadius { get; set; } = 400;

    [Range(50, 5000)]
    public int MaxRadius { get; set; } = 1500;

    /// <summary>How far to look for hero (Métro and RER/Train) stations, independently of the chosen radius.</summary>
    [Range(100, 5000)]
    public int HeroMaxDistance { get; set; } = 1000;
}
