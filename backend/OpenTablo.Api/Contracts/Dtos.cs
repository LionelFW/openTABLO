namespace OpenTablo.Api.Contracts;

/// <summary><paramref name="Realtime"/> is false when PRIM never has departures for this line.</summary>
public sealed record LineDto(string Id, string Name, string Mode, string Color, string TextColor, bool Realtime = true);

public sealed record StopAreaDto(string Id, string Name, string Type, double Lat, double Lon, int Distance, IReadOnlyList<LineDto> Lines);

public sealed record VelibStationDto(long Id, string Name, double Lat, double Lon, int Distance, int Mechanical, int Ebike, int Docks, bool Renting);

/// <summary><paramref name="Heroes"/> are the nearest station of each Métro and RER/Train line close by, and of each mode further away; they are not repeated in <paramref name="Stops"/>.</summary>
public sealed record NearbyResponse(int Radius, IReadOnlyList<StopAreaDto> Heroes, IReadOnlyList<StopAreaDto> Stops, IReadOnlyList<VelibStationDto> Velib);

/// <summary>Next planned departure of a line at a stop, from the theoretical timetable.</summary>
public sealed record ScheduledDto(string LineId, DateTimeOffset Planned);

/// <summary>Expected time is absolute so clients can count down without polling.</summary>
public sealed record DepartureDto(DateTimeOffset? Expected, bool AtStop, string? Status, string? Platform);

public sealed record DepartureGroupDto(string LineId, LineDto? Line, string Destination, IReadOnlyList<DepartureDto> Next);

public sealed record DeparturesResponse(string StopAreaId, DateTimeOffset FetchedAt, IReadOnlyList<DepartureGroupDto> Departures);

/// <summary><paramref name="Places"/> names the affected stops on this line, or the affected section ("A → B").</summary>
public sealed record DisruptionDto(string Id, string? Severity, string? Cause, string? Title, string? ShortMessage, string? Message, IReadOnlyList<string> Places);

public sealed record LineDisruptionsDto(string LineId, IReadOnlyList<DisruptionDto> Disruptions);

public sealed record AddressDto(string Label, double Lat, double Lon);

/// <summary>A station found by name: the stop areas of all its modes merged, with all their lines.</summary>
public sealed record StationDto(string Name, string? Town, double Lat, double Lon, IReadOnlyList<LineDto> Lines);

public sealed record PrimUsageDto(DateOnly Day, int Used, int Budget);
