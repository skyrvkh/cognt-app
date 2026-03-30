import { defineConfig } from 'vite';
import { fileURLToPath } from 'node:url';

export default defineConfig({
  base: './',
  build: {
    outDir: 'dist',
    emptyOutDir: true,
    sourcemap: false,
    target: 'es2022',
    rollupOptions: {
      input: {
        editor: fileURLToPath(new URL('./index.html', import.meta.url)),
        review: fileURLToPath(new URL('./review.html', import.meta.url)),
      },
    },
  },
});
