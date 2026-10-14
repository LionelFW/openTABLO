using OpenTablo.Api.Geocoding;

namespace OpenTablo.Api.Tests;

public class GeocodingClientTests
{
    static GeocoderFeature Feature(string label, string? cityCode) =>
        new(new GeocoderGeometry([2.35, 48.85]), new GeocoderProperties(label, cityCode));

    [Fact]
    public void Keeps_only_addresses_in_served_departments()
    {
        var addresses = GeocodingClient.ToAddresses(new GeocoderResponse([
            Feature("Rue de la Gare 45000 Orléans", "45234"),
            Feature("Rue de la Gare 75019 Paris", "75119"),
            Feature("Rue de la Gare 60200 Compiègne", "60159"),
            Feature("Rue de la Gare 95000 Cergy", "95127"),
            Feature("No code", null),
        ]));

        Assert.Equal(["Rue de la Gare 75019 Paris", "Rue de la Gare 60200 Compiègne"], addresses.Select(a => a.Label));
    }

    [Fact]
    public void Returns_at_most_five_addresses()
    {
        var many = Enumerable.Range(0, 30).Select(i => Feature($"{i} rue de Rivoli", "75101")).ToList();

        Assert.Equal(5, GeocodingClient.ToAddresses(new GeocoderResponse(many)).Count);
    }
}
