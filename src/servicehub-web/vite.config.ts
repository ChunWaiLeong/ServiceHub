import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'
import { resolveApiBaseUrl } from './api-config'

export default defineConfig(({ command, mode }) => {
  // Build commands require deployment configuration even with a custom mode name.
  if (command === 'build') resolveApiBaseUrl(loadEnv(mode, process.cwd(), 'VITE_').VITE_API_BASE_URL, true)
  return {
    plugins: [react()],
    server: { port: 5173, strictPort: true },
  }
})
