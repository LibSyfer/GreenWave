import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    // запросы /api уходят в GreenWave.Api (dotnet run --project src/GreenWave.Api)
    proxy: {
      '/api': 'http://localhost:5144',
    },
  },
})
