import { defineConfig } from 'vitest/config';

// The build uses relative asset paths, so the same dist/ works served by the game server at / and on
// GitHub Pages under /<repository>/. `npm run dev` proxies the game socket to a local server
// (HOLLOWWAKE_SERVER overrides where), so the page and the socket share an origin as in production.
const server = process.env.HOLLOWWAKE_SERVER ?? 'http://localhost:7778';

export default defineConfig({
  base: './',
  build: {
    outDir: 'dist',
    emptyOutDir: true,
    target: 'es2022',
    sourcemap: true,
    chunkSizeWarningLimit: 1500,
  },
  server: {
    port: 5173,
    proxy: {
      '/ws': { target: server, ws: true, changeOrigin: true },
    },
  },
  test: {
    environment: 'node',
    include: ['src/**/*.test.ts'],
  },
});
