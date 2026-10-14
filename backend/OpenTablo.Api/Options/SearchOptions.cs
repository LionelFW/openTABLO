using System.ComponentModel.DataAnnotations;

namespace OpenTablo.Api.Options;

public sealed class SearchOptions
{
    public const string Section = "Search";

    [Range(50, 5000)]
    public int DefaultRadius { get; set; } = 400;

    [Range(50, 5000)]
    public int MaxRadius { get; set; } = 1500;
}
