import { defineConfig } from 'vite';
import { fileURLToPath } from 'node:url';

export default defineConfig({
  base: './',
  resolve: {
    preserveSymlinks: true,
  },
  build: {
    outDir: 'dist',
    emptyOutDir: true,
    sourcemap: false,
    target: 'es2022',
    cssCodeSplit: false,
    rollupOptions: {
      preserveEntrySignatures: 'strict',
      input: {
        blazorBlocksuite: fileURLToPath(new URL('./src/blazor-blocksuite.ts', import.meta.url)),
      },
      output: {
        entryFileNames: 'assets/blazor-blocksuite.js',
        assetFileNames: assetInfo =>
          assetInfo.name === 'style.css'
            ? 'assets/style.css'
            : 'assets/[name]-[hash][extname]',
      },
    },
  },
});
