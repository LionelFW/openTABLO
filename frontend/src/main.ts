import './style.css'
import { api, ApiError, type DepartureGroup, type Disruption, type LineDisruptions, type StopArea, type VelibStation, type Line } from './api'
import { locateOnce } from './location'

/** Stops whose departures load automatically; others load on demand to save the PRIM quota. */
const AUTO_LOADED_STOPS = 3
const DEPARTURES_REFRESH_MS = 60_000
const COUNTDOWN_TICK_MS = 15_000
const RADIUS_KEY = 'opentablo.radius'
const HIDDEN_MODES_KEY = 'opentablo.hiddenModes'

const $ = <T extends HTMLElement>(id: string) => document.getElementById(id) as T

const state = {
  place: null as { lat: number; lon: number; label: string } | null,
  radius: Number(localStorage.getItem(RADIUS_KEY)) || 400,
  hiddenModes: new Set<string>(JSON.parse(localStorage.getItem(HIDDEN_MODES_KEY) ?? '[]')),
  openStops: new Set<string>(),
  departures: new Map<string, DepartureGroup[] | Error>(),
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
  return `<span class="badge badge-${mode}" style="--c:${line.color};--t:${line.textColor}" role="img" aria-label="${escapeHtml(label)}">${escapeHtml(line.name)}</span>`
}

function minutesLabel(expected: string | null, atStop: boolean): { text: string; sr: string; cls: string } {
  if (atStop) return { text: 'À quai', sr: 'à quai', cls: 'now' }
  if (!expected) return { text: '—', sr: 'horaire inconnu', cls: '' }
  const min = Math.floor((new Date(expected).getTime() - Date.now()) / 60_000)
  if (min <= 0) return { text: "À l'approche", sr: "à l'approche", cls: 'now' }
  return { text: `${min}<small>min</small>`, sr: `dans ${min} minutes`, cls: '' }
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
  if (data.length === 0) return `<p class="muted">Aucun passage prévu dans les deux prochaines heures.</p>`

  return `<ul class="departures">${data.map(g => `
    <li class="departure">
      ${lineBadge(g.line, g.lineId)}
      <span class="destination">${escapeHtml(g.destination)}</span>
      <span class="times">${g.next.slice(0, 2).map(d => {
        const t = minutesLabel(d.expected, d.atStop)
        return `<span class="time ${t.cls}" aria-label="${t.sr}">${t.text}</span>`
      }).join('')}</span>
    </li>`).join('')}</ul>`
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
  return status ? `<span class="status-wrap">${lineBadge(line, line.id)}<span class="status-dot sev-${status.toLowerCase()}" aria-label="trafic ${status === 'BLOQUANTE' ? 'interrompu' : status === 'PERTURBEE' ? 'perturbé' : 'info'}"></span></span>` : lineBadge(line, line.id)
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
        <li class="disruption sev-${severityOf(d).toLowerCase()}">
          <details>
            <summary>${lineBadge(line, line.id)} <span>${escapeHtml(d.title ?? d.shortMessage ?? 'Info trafic')}</span></summary>
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
          <span class="stop-meta">${MODE_LABEL[stop.type] ?? stop.type} · ${formatDistance(stop.distance)}</span>
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
    state.departures.set(stopId, (await api.departures(stopId)).departures)
  } catch (e) {
    state.departures.set(stopId, e instanceof ApiError && e.status === 429
      ? new Error('Quota de données temps réel atteint, réessayez plus tard.')
      : new Error('Prochains passages indisponibles.'))
  }
  renderStopBody(stopId)
}

async function loadDisruptions() {
  const lineIds = [...new Set([...heroes, ...stops].flatMap(s => s.lines.map(l => l.id)))]
  if (lineIds.length === 0) return
  try {
    state.disruptions = new Map((await api.disruptions(lineIds.slice(0, 200))).map(l => [l.lineId, l.disruptions]))
    renderTransit()
  } catch { /* traffic info is optional; departures still show */ }
}

// ---------- Vélib ----------

function renderVelib(stations: VelibStation[]) {
  $('velib').innerHTML = stations.length
    ? `<ul class="velib-list">${stations.map(s => `
        <li class="velib ${s.renting ? '' : 'closed'}">
          <span class="velib-name">${escapeHtml(s.name)}<small>${formatDistance(s.distance)}${s.renting ? '' : ' · hors service'}</small></span>
          <span class="velib-counts">
            <span class="count mech" aria-label="${s.mechanical} vélos mécaniques"><b>${s.mechanical}</b> méca.</span>
            <span class="count ebike" aria-label="${s.ebike} vélos électriques"><b>${s.ebike}</b> élec.</span>
            <span class="count docks" aria-label="${s.docks} places libres"><b>${s.docks}</b> places</span>
          </span>
        </li>`).join('')}</ul>`
    : `<p class="muted">Aucune station Vélib' dans ce rayon.</p>`
}

// ---------- Flow ----------

async function search() {
  if (!state.place) return
  $('welcome').hidden = true
  $('results').hidden = false
  $('change-place').hidden = false
  $('place-label').textContent = state.place.label
  $('transit').innerHTML = `<p class="muted">Recherche des arrêts…</p>`

  try {
    const res = await api.nearby(state.place.lat, state.place.lon, state.radius)
    heroes = res.heroes
    stops = res.stops
    state.departures.clear()
    state.openStops = new Set(shown([...heroes, ...stops]).slice(0, AUTO_LOADED_STOPS).map(s => s.id))
    renderTransit()
    renderVelib(res.velib)
    await Promise.all([...state.openStops].map(loadDepartures).concat(loadDisruptions()))
  } catch {
    $('transit').innerHTML = `<p class="notice">Impossible de charger les arrêts. Vérifiez votre connexion.</p>`
  }
}

function showWelcome() {
  state.place = null
  heroes = []
  stops = []
  $('welcome').hidden = false
  $('results').hidden = true
  $('change-place').hidden = true
}

$('use-location').addEventListener('click', async () => {
  const error = $('location-error')
  error.hidden = true
  const res = await locateOnce()
  if (res.ok) {
    state.place = { lat: res.lat, lon: res.lon, label: 'Autour de vous' }
    return search()
  }
  error.textContent = res.reason === 'denied'
    ? "L'accès à la position a été refusé. Vous pouvez saisir une adresse ci-dessous, ou autoriser la localisation dans les réglages."
    : 'Position indisponible pour le moment. Saisissez une adresse ci-dessous.'
  error.hidden = false
  $('address-input').focus()
})

// Address autocomplete, debounced so typing does not flood the geocoder.
let geocodeTimer: number | undefined
let geocodeAbort: AbortController | undefined
$('address-input').addEventListener('input', e => {
  const q = (e.target as HTMLInputElement).value.trim()
  clearTimeout(geocodeTimer)
  geocodeAbort?.abort()
  const list = $('address-results')
  if (q.length < 3) { list.innerHTML = ''; return }
  geocodeTimer = window.setTimeout(async () => {
    geocodeAbort = new AbortController()
    try {
      const results = await api.geocode(q, geocodeAbort.signal)
      list.innerHTML = results.map((a, i) =>
        `<li role="option"><button type="button" data-i="${i}">${escapeHtml(a.label)}</button></li>`).join('')
      list.querySelectorAll<HTMLButtonElement>('button').forEach(b => b.addEventListener('click', () => {
        const a = results[Number(b.dataset.i)]
        state.place = { lat: a.lat, lon: a.lon, label: a.label }
        list.innerHTML = ''
        search()
      }))
    } catch { /* aborted or offline: keep previous suggestions */ }
  }, 300)
})
$('address-form').addEventListener('submit', e => {
  e.preventDefault()
  $('address-results').querySelector<HTMLButtonElement>('button')?.click()
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

$('transit').addEventListener('click', e => {
  const id = (e.target as HTMLElement).closest<HTMLElement>('[data-toggle]')?.dataset.toggle
  if (!id) return
  if (state.openStops.delete(id)) return renderTransit()
  state.openStops.add(id)
  renderTransit()
  if (!state.departures.has(id)) loadDepartures(id)
})

for (const [tab, panel] of [['tab-transit', 'transit'], ['tab-velib', 'velib']] as const) {
  $(tab).addEventListener('click', () => {
    for (const [t, p] of [['tab-transit', 'transit'], ['tab-velib', 'velib']] as const) {
      $(t).setAttribute('aria-selected', String(t === tab))
      $(p).hidden = p !== panel
    }
    $('mode-filter').hidden = panel !== 'transit'
  })
}

// Countdowns update locally from absolute times; only open stops are refreshed, and only while visible.
setInterval(() => state.openStops.forEach(renderStopBody), COUNTDOWN_TICK_MS)
setInterval(() => {
  if (document.visibilityState === 'visible' && state.place) state.openStops.forEach(loadDepartures)
}, DEPARTURES_REFRESH_MS)
