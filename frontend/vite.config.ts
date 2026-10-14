import { defineConfig } from 'vite'

export default defineConfig({
  // In development the API runs separately; in production it serves this build from the same origin.
  server: { proxy: { '/api': 'http://localhost:5183' } },
})
