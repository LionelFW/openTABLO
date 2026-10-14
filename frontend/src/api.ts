// Mirrors backend/OpenTablo.Api/Contracts/Dtos.cs.

export interface Line { id: string; name: string; mode: string; color: string; textColor: string }
export interface StopArea { id: string; name: string; type: string; lat: number; lon: number; distance: number; lines: Line[] }
export interface VelibStation { id: number; name: string; lat: number; lon: number; distance: number; mechanical: number; ebike: number; docks: number; renting: boolean }
/** heroes are the nearest Métro and RER/Train stations, never repeated in stops. */
export interface NearbyResponse { radius: number; heroes: StopArea[]; stops: StopArea[]; velib: VelibStation[] }
export interface Departure { expected: string | null; atStop: boolean; status: string | null; platform: string | null }
export interface DepartureGroup { lineId: string; line: Line | null; destination: string; next: Departure[] }
export interface DeparturesResponse { stopAreaId: string; fetchedAt: string; departures: DepartureGroup[] }
export interface Disruption { id: string; severity: string | null; cause: string | null; title: string | null; shortMessage: string | null; message: string | null; places: string[] }
export interface LineDisruptions { lineId: string; disruptions: Disruption[] }
export interface Address { label: string; lat: number; lon: number }
/** A station found by name, all its modes merged. */
export interface Station { name: string; town: string | null; lat: number; lon: number; lines: Line[] }

export class ApiError extends Error {
  readonly status: number
  constructor(status: number, message: string) {
    super(message)
    this.status = status
  }
}

// Empty on the web (same origin as the API); set VITE_API_BASE for native builds.
const base = import.meta.env.VITE_API_BASE ?? ''

async function get<T>(path: string, signal?: AbortSignal): Promise<T> {
  // Without a timeout a stalled mobile connection leaves the "loading" text up forever.
  // Geocoding keeps its own signal: it is aborted by the next keystroke anyway.
  const res = await fetch(`${base}/api${path}`, { signal: signal ?? AbortSignal.timeout(15_000) })
  if (!res.ok) {
    const body = await res.json().catch(() => null)
    throw new ApiError(res.status, body?.title ?? `Erreur ${res.status}`)
  }
  return res.json() as Promise<T>
}

// ~11 m precision is plenty to list stops and avoids sending an exact position.
const round = (n: number) => n.toFixed(4)

export const api = {
  nearby: (lat: number, lon: number, radius: number) =>
    get<NearbyResponse>(`/nearby?lat=${round(lat)}&lon=${round(lon)}&radius=${radius}`),
  departures: (stopAreaId: string) => get<DeparturesResponse>(`/departures/${encodeURIComponent(stopAreaId)}`),
  disruptions: (lineIds: string[]) => get<LineDisruptions[]>(`/disruptions?lines=${lineIds.map(encodeURIComponent).join(',')}`),
  stations: (q: string, signal?: AbortSignal) => get<Station[]>(`/stations?q=${encodeURIComponent(q)}`, signal),
  geocode: (q: string, signal?: AbortSignal) => get<Address[]>(`/geocode?q=${encodeURIComponent(q)}`, signal),
}
