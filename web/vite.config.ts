import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    // запросы /api уходят в GreenWave.Api (dotnet run --project src/GreenWave.Api);
    // другой адрес API можно задать переменной окружения API_URL
    proxy: {
      '/api': process.env.API_URL ?? 'http://localhost:5144',
    },
  },
})
