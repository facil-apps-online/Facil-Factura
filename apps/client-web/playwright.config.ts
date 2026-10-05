import { defineConfig } from '@playwright/test';

const PORT = 5199;

// Las pruebas corren contra el build de producción servido con `vite preview` (más estable que el
// servidor de desarrollo, que re-optimiza dependencias en la primera carga). La API se simula en
// cada prueba interceptando las peticiones a /api, por eso VITE_API_URL apunta al mismo origen.
export default defineConfig({
  testDir: './e2e',
  timeout: 60_000,
  fullyParallel: true,
  workers: 4,
  retries: 0,
  reporter: [['list']],
  use: {
    baseURL: `http://localhost:${PORT}`,
    browserName: 'chromium',
  },
  webServer: {
    command: `npx vite build && npx vite preview --port ${PORT} --strictPort`,
    url: `http://localhost:${PORT}`,
    reuseExistingServer: false,
    timeout: 240_000,
    env: { VITE_API_URL: `http://localhost:${PORT}/api` },
  },
});
