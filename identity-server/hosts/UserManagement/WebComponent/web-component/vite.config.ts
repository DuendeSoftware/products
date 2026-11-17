import { defineConfig } from 'vite';

export default defineConfig({
  build: {
    outDir: '../AuthenticationFlowWithWebComponent/wwwroot/dist',
    minify: 'terser',
    sourcemap: false,
    lib: {
      entry: 'src/auth-component.ts',
      formats: ['es'],
      fileName: 'auth-component'
    },
    rollupOptions: {
      external: [],
      output: {
        inlineDynamicImports: true,
        manualChunks: undefined
      }
    },
    terserOptions: {
      compress: {
        drop_console: true,
        drop_debugger: true
      }
    }
  }
});
