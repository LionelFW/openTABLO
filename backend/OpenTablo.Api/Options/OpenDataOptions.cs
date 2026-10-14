using System.ComponentModel.DataAnnotations;

namespace OpenTablo.Api.Options;

public sealed class OpenDataOptions
{
    public const string Section = "OpenData";

    [Required, Url]
    public string IdfmBaseUrl { get; set; } = "https://data.iledefrance-mobilites.fr/api/explore/v2.1/catalog/datasets/";

    [Required, Url]
    public string VelibBaseUrl { get; set; } = "https://velib-metropole-opendata.smovengo.cloud/opendata/Velib_Metropole/";

    [Required, Url]
    public string GeocoderBaseUrl { get; set; } = "https://data.geopf.fr/geocodage/";

    /// <summary>How old the stored referential may get before it is downloaded again.</summary>
    [Range(typeof(TimeSpan), "01:00:00", "30.00:00:00")]
    public TimeSpan ReferentialMaxAge { get; set; } = TimeSpan.FromDays(1);

    [Range(10, 600)]
    public int VelibStatusCacheSeconds { get; set; } = 60;
}
