namespace OpenTablo.Api.Contracts;

public sealed record LineDto(string Id, string Name, string Mode, string Color, string TextColor);

public sealed record StopAreaDto(string Id, string Name, string Type, double Lat, double Lon, int Distance, IReadOnlyList<LineDto> Lines);

public sealed record VelibStationDto(long Id, string Name, double Lat, double Lon, int Distance, int Mechanical, int Ebike, int Docks, bool Renting);

public sealed record NearbyResponse(int Radius, IReadOnlyList<StopAreaDto> Stops, IReadOnlyList<VelibStationDto> Velib);

/// <summary>Expected time is absolute so clients can count down without polling.</summary>
public sealed record DepartureDto(DateTimeOffset? Expected, bool AtStop, string? Status, string? Platform);

public sealed record DepartureGroupDto(string LineId, LineDto? Line, string Destination, IReadOnlyList<DepartureDto> Next);

public sealed record DeparturesResponse(string StopAreaId, DateTimeOffset FetchedAt, IReadOnlyList<DepartureGroupDto> Departures);

public sealed record DisruptionDto(string Id, string? Severity, string? Cause, string? Title, string? ShortMessage, string? Message);

public sealed record LineDisruptionsDto(string LineId, IReadOnlyList<DisruptionDto> Disruptions);

public sealed record AddressDto(string Label, double Lat, double Lon);

public sealed record PrimUsageDto(DateOnly Day, int Used, int Budget);
