namespace OpenTablo.Api.Velib;

public static class GbfsStationStatusExtensions
{
    public static int BikesOfType(this GbfsStationStatus status, string type) =>
        status.BikeTypes?.Sum(t => t.GetValueOrDefault(type)) ?? 0;
}
