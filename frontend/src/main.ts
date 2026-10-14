import './style.css'
import { api, ApiError, type Address, type DeparturesResponse, type Disruption, type LineDisruptions, type StopArea, type VelibStation, type Line } from './api'
import { locateOnce, locationGranted } from './location'

/** Stops whose departures load automatically; others load on demand to save the PRIM quota. */
const AUTO_LOADED_STOPS = 3
const DEPARTURES_REFRESH_MS = 60_000
const COUNTDOWN_TICK_MS = 15_000
const RADIUS_KEY = 'opentablo.radius'
const HIDDEN_MODES_KEY = 'opentablo.hiddenModes'
const LAST_PLACE_KEY = 'opentablo.lastPlace'

const $ = <T extends HTMLElement>(id: string) => document.getElementById(id) as T

const state = {
  place: null as { lat: number; lon: number; label: string } | null,
  radius: Number(localStorage.getItem(RADIUS_KEY)) || 400,
  hiddenModes: new Set<string>(JSON.parse(localStorage.getItem(HIDDEN_MODES_KEY) ?? '[]')),
  openStops: new Set<string>(),
  departures: new Map<string, DeparturesResponse | Error>(),
  disruptions: new Map<string, LineDisruptions['disruptions']>(),
}

// ---------- Rendering helpers ----------

const escapeHtml = (s: string) =>
  s.replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]!)

/** IDFM messages are HTML; only their text is shown, never their markup. */
const htmlToText = (html: string) => new DOMParser().parseFromString(html, 'text/html').body.textContent?.trim() ?? ''

const MODE_LABEL: Record<string, string> = {
  metro: 'Métro', rail: 'RER / Train', tram: 'Tramway', bus: 'Bus', funicular: 'Funiculaire', cableway: 'Câble',
}

function lineBadge(line: Line | null, fallbackId: string): string {
  if (!line) return `<span class="badge badge-bus" style="--c:#6e6e6e;--t:#fff">${escapeHtml(fallbackId)}</span>`
  const mode = ['metro', 'rail', 'tram'].includes(line.mode) ? line.mode : 'bus'
  const label = `${MODE_LABEL[line.mode] ?? 'Ligne'} ${line.name}`
  return `<span class="badge badge-${mode}" style="--c:${escapeHtml(line.color)};--t:${escapeHtml(line.textColor)}" role="img" aria-label="${escapeHtml(label)}">${escapeHtml(line.name)}</span>`
}

function minutesLabel(expected: string | null, atStop: boolean): { text: string; sr: string; cls: string } {
  if (atStop) return { text: 'À quai', sr: 'à quai', cls: 'now' }
  if (!expected) return { text: '—', sr: 'horaire inconnu', cls: '' }
  const min = Math.floor((new Date(expected).getTime() - Date.now()) / 60_000)
  if (min <= 0) return { text: "À l'approche", sr: "à l'approche", cls: 'now' }
  return { text: `${min}<small>min</small>`, sr: `dans ${min} minutes`, cls: '' }
}

function updatedLabel(fetchedAt: string) {
  const min = Math.floor((Date.now() - Date.parse(fetchedAt)) / 60_000)
  return min < 1 ? "Mis à jour à l'instant" : `Mis à jour il y a ${min} min`
}

function formatDistance(m: number) {
  return m < 1000 ? `${m} m` : `${(m / 1000).toFixed(1).replace('.', ',')} km`
}

// ---------- Transit ----------

function renderDepartures(stop: StopArea): string {
  const data = state.departures.get(stop.id)
  if (!state.openStops.has(stop.id)) return ''
  if (data === undefined) return `<p class="muted">Chargement des prochains passages…</p>`
  if (data instanceof Error) return `<p class="notice">${escapeHtml(data.message)}</p>`
  // Cached data can be a minute old, or older after a failed refresh: drop trains already gone.
  const groups = data.departures
    .map(g => ({ ...g, next: g.next.filter(d => d.atStop || !d.expected || Date.parse(d.expected) > Date.now() - 60_000) }))
    .filter(g => g.next.length > 0)
  if (groups.length === 0) return `<p class="muted">Aucun passage prévu dans les deux prochaines heures.</p>`

  return `<ul class="departures">${groups.map(g => `
    <li class="departure">
      ${lineBadge(g.line, g.lineId)}
      <span class="destination">${escapeHtml(g.destination)}</span>
      <span class="times">${g.next.slice(0, 2).map(d => {
        const t = minutesLabel(d.expected, d.atStop)
        return `<span class="time ${t.cls}" aria-label="${t.sr}">${t.text}</span>`
      }).join('')}</span>
    </li>`).join('')}</ul>
    <p class="updated">${updatedLabel(data.fetchedAt)}</p>`
}

const SEVERITY_RANK: Record<string, number> = { BLOQUANTE: 0, PERTURBEE: 1, INFORMATION: 2 }
const severityOf = (d: Disruption) => d.severity ?? 'INFORMATION'

/** Worst live incident on a line, shown as a dot on its pictogram. Planned works only appear in the list: nearly every bus line has some. */
function lineStatus(line: Line): string | null {
  const list = (state.disruptions.get(line.id) ?? []).filter(d => d.cause === 'PERTURBATION')
  return list.map(severityOf).sort((a, b) => (SEVERITY_RANK[a] ?? 9) - (SEVERITY_RANK[b] ?? 9))[0] ?? null
}

function lineWithStatus(line: Line): string {
  const status = lineStatus(line)
  return status ? `<span class="status-wrap">${lineBadge(line, line.id)}<span class="status-dot sev-${escapeHtml(status.toLowerCase())}" role="img" aria-label="trafic ${status === 'BLOQUANTE' ? 'interrompu' : status === 'PERTURBEE' ? 'perturbé' : 'info'}"></span></span>` : lineBadge(line, line.id)
}

/** Traffic info for the stop's lines, collapsed by default: PRIM reports issues anywhere on a line. */
function renderDisruptions(stop: StopArea): string {
  const byId = new Map<string, { line: Line; d: Disruption }>()
  for (const line of stop.lines)
    for (const d of state.disruptions.get(line.id) ?? [])
      if (!byId.has(d.id)) byId.set(d.id, { line, d })
  if (byId.size === 0) return ''

  const items = [...byId.values()].sort((a, b) => (SEVERITY_RANK[severityOf(a.d)] ?? 9) - (SEVERITY_RANK[severityOf(b.d)] ?? 9))
  return `
    <details class="traffic">
      <summary>Info trafic <span class="traffic-count">${items.length}</span></summary>
      <ul class="disruptions">${items.map(({ line, d }) => `
        <li class="disruption sev-${escapeHtml(severityOf(d).toLowerCase())}">
          <details>
            <summary>${lineBadge(line, line.id)} <span>${escapeHtml(d.title ?? d.shortMessage ?? 'Info trafic')}${d.places.length
              ? `<small class="places">${escapeHtml(d.places.join(', '))}</small>` : ''}</span></summary>
            <p>${escapeHtml(htmlToText(d.message ?? d.shortMessage ?? ''))}</p>
          </details>
        </li>`).join('')}</ul>
    </details>`
}

function renderStop(stop: StopArea, hero = false): string {
  const open = state.openStops.has(stop.id)
  return `
    <article class="stop${hero ? ' hero' : ''}" data-stop="${escapeHtml(stop.id)}">
      <button class="stop-head" aria-expanded="${open}" data-toggle="${escapeHtml(stop.id)}">
        <span class="stop-title">
          <span class="stop-name">${escapeHtml(stop.name)}</span>
          <span class="stop-meta">${MODE_LABEL[stop.type] ?? escapeHtml(stop.type)}, à ${formatDistance(stop.distance)}</span>
        </span>
        <span class="stop-lines">${stop.lines.map(lineWithStatus).join('')}</span>
      </button>
      ${renderDisruptions(stop)}
      <div class="stop-body">${renderDepartures(stop)}</div>
    </article>`
}

let heroes: StopArea[] = []
let stops: StopArea[] = []

const shown = (list: StopArea[]) => list.filter(s => !state.hiddenModes.has(s.type))

function renderTransit() {
  const h = shown(heroes), s = shown(stops)
  $('transit').innerHTML = h.length + s.length
    ? h.map(x => renderStop(x, true)).join('') + s.map(x => renderStop(x)).join('')
    : `<p class="muted">Aucun arrêt dans ce rayon. Essayez un rayon plus grand ou d'autres modes.</p>`
}

/** Updates one stop's departures without touching the rest (keeps open disruption details open). */
function renderStopBody(stopId: string) {
  const stop = [...heroes, ...stops].find(s => s.id === stopId)
  const body = document.querySelector(`[data-stop="${CSS.escape(stopId)}"] .stop-body`)
  if (stop && body) body.innerHTML = renderDepartures(stop)
}

async function loadDepartures(stopId: string) {
  try {
    state.departures.set(stopId, await api.departures(stopId))
  } catch (e) {
    const previous = state.departures.get(stopId)
    if (previous && !(previous instanceof Error)) return // keep the last good data on a failed refresh
    state.departures.set(stopId, e instanceof ApiError && e.status === 429
      ? new Error('Quota de données temps réel atteint, réessayez plus tard.')
      : new Error('Prochains passages indisponibles.'))
  }
  renderStopBody(stopId)
}

async function loadDisruptions() {
  const id = searchId
  const lineIds = [...new Set([...heroes, ...stops].flatMap(s => s.lines.map(l => l.id)))]
  if (lineIds.length === 0) return
  try {
    const res = await api.disruptions(lineIds.slice(0, 200))
    if (id !== searchId) return
    state.disruptions = new Map(res.map(l => [l.lineId, l.disruptions]))
    renderTransit()
  } catch { /* traffic info is optional; departures still show */ }
}

// ---------- Vélib ----------

function renderVelib(stations: VelibStation[]) {
  $('velib').innerHTML = stations.length
    ? `<ul class="velib-list">${stations.map(s => `
        <li class="velib ${s.renting ? '' : 'closed'}">
          <span class="velib-name">${escapeHtml(s.name)}<small>À ${formatDistance(s.distance)}${s.renting ? '' : ', hors service'}</small></span>
          <span class="velib-counts">
            <span class="count mech" aria-label="${s.mechanical} vélos mécaniques"><b>${s.mechanical}</b> méca.</span>
            <span class="count ebike" aria-label="${s.ebike} vélos électriques"><b>${s.ebike}</b> élec.</span>
            <span class="count docks" aria-label="${s.docks} places libres"><b>${s.docks}</b> places</span>
          </span>
        </li>`).join('')}</ul>`
    : `<p class="muted">Aucune station Vélib' dans ce rayon.</p>`
}

// ---------- Flow ----------

/** Bumped on every search so a slow, superseded response never overwrites a newer one. */
let searchId = 0

async function search() {
  if (!state.place) return
  const id = ++searchId
  $('welcome').hidden = true
  $('results').hidden = false
  $('place-actions').hidden = false
  $('relocate-error').hidden = true
  $('place-label').textContent = state.place.label
  $('transit').innerHTML = `<p class="muted">Recherche des arrêts…</p>`

  try {
    const res = await api.nearby(state.place.lat, state.place.lon, state.radius)
    if (id !== searchId) return
    heroes = res.heroes
    stops = res.stops
    state.departures.clear()
    state.openStops = new Set(shown([...heroes, ...stops]).slice(0, AUTO_LOADED_STOPS).map(s => s.id))
    renderTransit()
    renderVelib(res.velib)
    await Promise.all([...state.openStops].map(loadDepartures).concat(loadDisruptions()))
  } catch {
    if (id !== searchId) return
    $('transit').innerHTML = `<p class="notice">Impossible de charger les arrêts. Vérifiez votre connexion.</p>`
  }
}

function showWelcome() {
  localStorage.removeItem(LAST_PLACE_KEY)
  state.place = null
  searchId++
  addressInput.value = ''
  addressList.innerHTML = ''
  heroes = []
  stops = []
  $('welcome').hidden = false
  $('results').hidden = true
  $('place-actions').hidden = true
}

/** Works from the welcome screen and, as a position refresh, from the stops display. */
async function useLocation() {
  const onWelcome = !$('welcome').hidden
  const error = $(onWelcome ? 'location-error' : 'relocate-error')
  error.hidden = true
  const buttons = [$<HTMLButtonElement>('use-location'), $<HTMLButtonElement>('relocate')]
  buttons.forEach(b => { b.disabled = true; b.setAttribute('aria-busy', 'true') })
  const res = await locateOnce()
  buttons.forEach(b => { b.disabled = false; b.removeAttribute('aria-busy') })
  if (res.ok) {
    localStorage.removeItem(LAST_PLACE_KEY) // the position now replaces the typed address on next start
    state.place = { lat: res.lat, lon: res.lon, label: 'Autour de vous' }
    return search()
  }
  if (onWelcome) {
    error.textContent = res.reason === 'denied'
      ? "L'accès à la position a été refusé. Vous pouvez saisir une station ou une adresse ci-dessous, ou autoriser la localisation dans les réglages."
      : 'Position indisponible pour le moment. Saisissez une station ou une adresse ci-dessous.'
    $('address-input').focus()
  } else {
    error.textContent = res.reason === 'denied'
      ? "L'accès à la position a été refusé. Autorisez la localisation dans les réglages."
      : 'Position indisponible pour le moment.'
  }
  error.hidden = false
}
$('use-location').addEventListener('click', useLocation)
$('relocate').addEventListener('click', useLocation)

const addressInput = $<HTMLInputElement>('address-input')
const addressList = $('address-results')

/** Stations come with their lines; addresses do not. */
type Suggestion = Address & { town?: string | null; lines?: Line[] }

const MAX_SUGGESTED_LINES = 6

function suggestionHtml(s: Suggestion, i: number): string {
  if (!s.lines) return `<li role="option"><button type="button" data-i="${i}">${escapeHtml(s.label)}</button></li>`
  // Métro, RER and Tram identify a station; its bus numbers would only crowd the row.
  const heavy = s.lines.filter(l => l.mode !== 'bus')
  const lines = heavy.length ? heavy : s.lines
  const more = lines.length - MAX_SUGGESTED_LINES
  return `<li role="option"><button type="button" class="station" data-i="${i}">
    <span class="station-name">${escapeHtml(s.label)}${s.town ? `<small>${escapeHtml(s.town)}</small>` : ''}</span>
    <span class="station-lines">${lines.slice(0, MAX_SUGGESTED_LINES).map(l => lineBadge(l, l.id)).join('')}${more > 0 ? `<span class="more">+${more}</span>` : ''}</span>
  </button></li>`
}

function pickAddress(a: Address) {
  state.place = { lat: a.lat, lon: a.lon, label: a.label }
  localStorage.setItem(LAST_PLACE_KEY, JSON.stringify(state.place))
  addressList.innerHTML = ''
  search()
}

let geocodeTimer: number | undefined
let geocodeAbort: AbortController | undefined

/** Fetches and lists stations then addresses for q; resolves to [] when a newer keystroke aborted it or both lookups failed. */
async function suggest(q: string): Promise<Suggestion[]> {
  geocodeAbort?.abort()
  const abort = geocodeAbort = new AbortController()
  // Either source alone is still useful, so one failing does not hide the other.
  const [stations, addresses] = await Promise.allSettled([api.stations(q, abort.signal), api.geocode(q, abort.signal)])
  if (abort.signal.aborted) return []
  if (stations.status === 'rejected' && addresses.status === 'rejected') {
    addressList.innerHTML = `<li class="muted">Recherche indisponible. Vérifiez votre connexion.</li>`
    return []
  }
  const results: Suggestion[] = [
    ...(stations.status === 'fulfilled' ? stations.value : []).map(s => ({ label: s.name, town: s.town, lat: s.lat, lon: s.lon, lines: s.lines })),
    ...(addresses.status === 'fulfilled' ? addresses.value : []),
  ]
  addressList.dataset.q = q
  addressList.innerHTML = results.length ? results.map(suggestionHtml).join('') : `<li class="muted">Aucune station ni adresse trouvée.</li>`
  addressList.querySelectorAll<HTMLButtonElement>('button').forEach(b =>
    b.addEventListener('click', () => pickAddress(results[Number(b.dataset.i)])))
  return results
}

// Station and address autocomplete, debounced so typing does not flood the geocoder.
addressInput.addEventListener('input', () => {
  const q = addressInput.value.trim()
  clearTimeout(geocodeTimer)
  geocodeAbort?.abort()
  if (q.length < 3) { addressList.innerHTML = ''; return }
  geocodeTimer = window.setTimeout(() => suggest(q), 300)
})

// Enter picks the first suggestion, fetching it first if the list is missing or was for an older text.
$('address-form').addEventListener('submit', async e => {
  e.preventDefault()
  clearTimeout(geocodeTimer)
  const q = addressInput.value.trim()
  if (q.length < 3) return
  const current = addressList.dataset.q === q ? addressList.querySelector<HTMLButtonElement>('button') : null
  if (current) return current.click()
  const [first] = await suggest(q)
  if (first) pickAddress(first)
})

$('change-place').addEventListener('click', showWelcome)

const radiusSelect = $('radius') as HTMLSelectElement
radiusSelect.value = String(state.radius)
radiusSelect.addEventListener('change', () => {
  state.radius = Number(radiusSelect.value)
  localStorage.setItem(RADIUS_KEY, String(state.radius))
  search()
})

// Filtering is local: no new request, and hidden stops stop refreshing their departures.
const modeButtons = document.querySelectorAll<HTMLButtonElement>('#mode-filter [data-mode]')
modeButtons.forEach(b => {
  b.setAttribute('aria-pressed', String(!state.hiddenModes.has(b.dataset.mode!)))
  b.addEventListener('click', () => {
    const mode = b.dataset.mode!
    if (!state.hiddenModes.delete(mode)) state.hiddenModes.add(mode)
    b.setAttribute('aria-pressed', String(!state.hiddenModes.has(mode)))
    localStorage.setItem(HIDDEN_MODES_KEY, JSON.stringify([...state.hiddenModes]))
    for (const s of [...heroes, ...stops]) if (state.hiddenModes.has(s.type)) state.openStops.delete(s.id)
    renderTransit()
  })
})

// Toggles one stop in place, so traffic details opened elsewhere stay open.
$('transit').addEventListener('click', e => {
  const head = (e.target as HTMLElement).closest<HTMLElement>('[data-toggle]')
  const id = head?.dataset.toggle
  if (!head || !id) return
  const open = !state.openStops.delete(id)
  if (open) state.openStops.add(id)
  head.setAttribute('aria-expanded', String(open))
  renderStopBody(id)
  if (open && !state.departures.has(id)) loadDepartures(id)
})

const TABS = [['tab-transit', 'transit'], ['tab-velib', 'velib']] as const

function selectTab(index: number) {
  TABS.forEach(([t, p], i) => {
    $(t).setAttribute('aria-selected', String(i === index))
    $(t).tabIndex = i === index ? 0 : -1
    $(p).hidden = i !== index
  })
  $('mode-filter').hidden = $('radius-note').hidden = TABS[index][1] !== 'transit'
}

TABS.forEach(([t], i) => {
  $(t).addEventListener('click', () => selectTab(i))
  // Standard tablist keys: arrows move between tabs, only the selected one is in the Tab order.
  $(t).addEventListener('keydown', e => {
    const step = e.key === 'ArrowRight' ? 1 : e.key === 'ArrowLeft' ? -1 : 0
    if (!step) return
    const next = (i + step + TABS.length) % TABS.length
    selectTab(next)
    $(TABS[next][0]).focus()
  })
})

// Countdowns update locally from absolute times; only open stops are refreshed, and only while visible.
setInterval(() => state.openStops.forEach(renderStopBody), COUNTDOWN_TICK_MS)
let lastRefresh = Date.now()
function refreshOpenStops() {
  if (document.visibilityState !== 'visible' || !state.place) return
  lastRefresh = Date.now()
  state.openStops.forEach(loadDepartures)
}
setInterval(refreshOpenStops, DEPARTURES_REFRESH_MS)
// Back from the background: times may be minutes old, so refresh now instead of waiting for the next tick.
document.addEventListener('visibilitychange', () => {
  if (Date.now() - lastRefresh > DEPARTURES_REFRESH_MS / 2) refreshOpenStops()
})

// Start where the user left off: their last typed address, else their position if location is already allowed.
const lastPlace = (() => { try { return JSON.parse(localStorage.getItem(LAST_PLACE_KEY) ?? 'null') } catch { return null } })()
if (lastPlace) {
  state.place = lastPlace
  search()
} else {
  locationGranted().then(granted => {
    $('welcome').hidden = false
    if (granted) useLocation()
  })
}
