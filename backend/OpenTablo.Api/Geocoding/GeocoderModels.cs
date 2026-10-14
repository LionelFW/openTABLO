namespace OpenTablo.Api.Geocoding;

// GeoJSON returned by the Géoplateforme geocoder.

public sealed record GeocoderResponse(IReadOnlyList<GeocoderFeature> Features);

public sealed record GeocoderFeature(GeocoderGeometry Geometry, GeocoderProperties Properties);

/// <summary>Coordinates are [longitude, latitude].</summary>
public sealed record GeocoderGeometry(IReadOnlyList<double> Coordinates);

/// <summary><paramref name="CityCode"/> is the INSEE commune code; its first two digits are the department.</summary>
public sealed record GeocoderProperties(string Label, string? CityCode);
