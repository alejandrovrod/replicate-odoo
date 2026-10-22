import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { defineConfig } from 'vitest/config'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 5173,
    proxy: {
      // The API redirects HTTP->HTTPS (UseHttpsRedirection), so proxy straight to the
      // HTTPS endpoint: a 307 from the proxy target would land off-origin in the browser
      // and fail CORS. `secure: false` because Aspire's dev certificate is self-signed.
      '/api': {
        target: 'https://localhost:7244',
        changeOrigin: true,
        // Keep the original host visible to the API as X-Forwarded-Host, so
        // `acme.localhost:5173` resolves as a tenant subdomain in local development.
        xfwd: true,
        secure: false,
      },
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    include: ['src/**/*.test.{ts,tsx}'],
  },
})
