import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 3000,
    proxy: {
      "/api": {
        target: "http://localhost:5000",
        changeOrigin: true,
      },
    },
  },
  build: {
    chunkSizeWarningLimit: 600,
    rollupOptions: {
      output: {
        manualChunks(id) {
          // pnpm paths contain peer names (e.g. recharts@..._react-dom@...):
          // match the actual package after the final node_modules segment.
          const path = id.replace(/\\/g, "/");
          if (path.includes("commonjsHelpers.js")) return "vendor-runtime";
          const marker = "/node_modules/";
          const packageIndex = path.lastIndexOf(marker);
          if (packageIndex >= 0) {
            const packagePath = path.slice(packageIndex + marker.length);
            if (/^(?:react|react-dom|react-router|react-router-dom|scheduler)\//.test(packagePath)) {
              return "vendor-react";
            }
            if (/^(?:@tanstack\/|axios\/|zustand\/)/.test(packagePath)) {
              return "vendor-query";
            }
            if (packagePath.startsWith("katex/")) {
              return "vendor-katex";
            }
            if (packagePath.startsWith("mathlive/")) {
              return "vendor-mathlive";
            }
            if (packagePath.startsWith("xlsx/")) {
              return "vendor-xlsx";
            }
            if (/^(?:recharts\/|d3-[^/]+\/|victory-vendor\/)/.test(packagePath)) {
              return "vendor-recharts";
            }
          }
        },
      },
    },
  },
});
