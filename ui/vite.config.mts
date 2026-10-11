import { defineConfig } from 'vite';
import tailwindcss from '@tailwindcss/vite';
export default defineConfig({
  root: 'ui', plugins: [tailwindcss()],
  build: { outDir: '../src/AutoShine.Web/wwwroot', emptyOutDir: true },
  server: { host: '127.0.0.1', port: 5173, strictPort: true, proxy: {
    '/api': { target: 'http://127.0.0.1:48191', changeOrigin: true,
      configure(proxy) { proxy.on('proxyReq', req => {
        if (req.getHeader('origin') === 'http://127.0.0.1:5173') req.setHeader('origin', 'http://127.0.0.1:48191');
      }); },
    },
  } },
});
