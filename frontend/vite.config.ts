import { defineConfig, type Plugin } from 'vite'

/**
 * `npm run dev:mock`: departures and traffic info, the two endpoints that spend the daily PRIM quota,
 * are generated here. Nearby, stations and geocoding are free and still go to the API.
 */
function mockPrim(): Plugin {
  const metro1 = { id: 'C01371', name: '1', mode: 'metro', color: '#ffbe00', textColor: '#000000', realtime: true }
  const rerA = { id: 'C01742', name: 'A', mode: 'rail', color: '#eb2132', textColor: '#ffffff', realtime: true }
  const bus20 = { id: 'C01077', name: '20', mode: 'bus', color: '#9b9842', textColor: '#ffffff', realtime: true }
  const at = (min: number) => new Date(Date.now() + min * 60_000).toISOString()
  const next = (...mins: number[]) => mins.map(m => ({ expected: at(m), atStop: m === 0, status: null, platform: null }))

  return {
    name: 'mock-prim',
    configureServer(server) {
      server.middlewares.use((req, res, next_) => {
        const path = new URL(req.url ?? '/', 'http://localhost').pathname
        let body: unknown
        if (path.startsWith('/api/departures/')) {
          body = {
            stopAreaId: path.split('/').pop(),
            fetchedAt: new Date().toISOString(),
            departures: [
              { lineId: metro1.id, line: metro1, destination: 'La Défense (Grande Arche)', next: next(0, 4) },
              { lineId: metro1.id, line: metro1, destination: 'Château de Vincennes', next: next(0.5, 6) },
              { lineId: rerA.id, line: rerA, destination: 'Marne-la-Vallée Chessy', next: next(7, 19) },
              { lineId: bus20.id, line: bus20, destination: 'Gare Saint-Lazare', next: next(12, 31) },
            ],
          }
        } else if (path === '/api/disruptions') {
          body = [{
            lineId: metro1.id,
            disruptions: [{
              id: 'mock-1', severity: 'PERTURBEE', cause: 'PERTURBATION', title: 'Trafic perturbé (simulation)',
              shortMessage: null, message: 'Message de test du mode mock.', places: ['Nation → Château de Vincennes'],
            }],
          }]
        } else {
          return next_()
        }
        res.setHeader('Content-Type', 'application/json')
        res.end(JSON.stringify(body))
      })
    },
  }
}

export default defineConfig(({ mode }) => ({
  plugins: mode === 'mock' ? [mockPrim()] : [],
  // In development the API runs separately; in production it serves this build from the same origin.
  server: { proxy: { '/api': 'http://localhost:5183' } },
}))
