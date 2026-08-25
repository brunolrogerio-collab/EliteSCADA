import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

const backend = process.env.SCADA_API_PROXY ?? 'http://127.0.0.1:5080';

export default defineConfig({
  plugins: [react()],
  server: {
    host: '0.0.0.0',
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': { target: backend, changeOrigin: true },
      '/health': { target: backend, changeOrigin: true },
      '/openapi': { target: backend, changeOrigin: true },
      '/ws': { target: backend, changeOrigin: true, ws: true }
    }
  }
});
