import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// The API runs on its own (dotnet run --project src/BrightPath.Api). The dev server forwards /api to it,
// so the page and the API share one origin and no CORS setup is needed.
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': 'http://localhost:5238',
    },
  },
})
