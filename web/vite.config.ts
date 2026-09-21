import { fileURLToPath, URL } from 'node:url'

import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// Four services on four ports, and none of them sends CORS headers. Rather than open them up,
// the browser only ever talks to this dev server and the proxy makes the calls server-side, so
// the origin never changes. The production answer is the gateway in Adım 15; until then the app
// only runs behind Vite.
//
// The hubs need ws: true. Without it the WebSocket upgrade is not proxied and SignalR silently
// falls back to long polling, which works and hides the problem.
const services = {
  incident: 'http://localhost:5203',
  agent: 'http://localhost:5130',
  notification: 'http://localhost:5210',
  telemetry: 'http://localhost:5220',
}

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    proxy: {
      '/api/incidents': { target: services.incident, changeOrigin: true },
      '/api/analyses': { target: services.agent, changeOrigin: true },
      '/api/notifications': { target: services.notification, changeOrigin: true },
      '/api/integrations': { target: services.notification, changeOrigin: true },
      // Both telemetry prefixes land on the same service, so the fact that
      // /api/telemetry-sources also starts with /api/telemetry costs nothing.
      '/api/telemetry-sources': { target: services.telemetry, changeOrigin: true },
      '/api/telemetry': { target: services.telemetry, changeOrigin: true },
      '/hubs/incidents': { target: services.incident, changeOrigin: true, ws: true },
      '/hubs/notifications': { target: services.notification, changeOrigin: true, ws: true },
      '/hubs/signals': { target: services.telemetry, changeOrigin: true, ws: true },
    },
  },
})
