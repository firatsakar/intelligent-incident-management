import { fileURLToPath, URL } from 'node:url'

import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// One line, because the gateway is now the thing that knows there are four services. What used to
// be nine entries here lives in src/Gateway/Gateway.API/GatewayRoutes.cs, where the same table
// also works for a built dist/ — this one only ever existed while a dev server was running.
//
// ws: true still matters. Without it the WebSocket upgrade is not proxied and SignalR silently
// falls back to long polling, which works and hides the problem.
const gateway = 'http://localhost:5100'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    proxy: {
      '/api': { target: gateway, changeOrigin: true },
      '/hubs': { target: gateway, changeOrigin: true, ws: true },
      // Where the telemetry settings screen tells a collector to send, so it has to answer on the
      // console's own origin in development as it does behind the gateway in production.
      '/otlp': { target: gateway, changeOrigin: true },
    },
  },
})
