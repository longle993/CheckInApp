import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";
export default defineConfig({
  plugins: [react(), tailwindcss()],
  preview: {
    allowedHosts: ["longle993.io.vn"],
    proxy: {
      "/api": {
        target: process.env.API_URL || "http://127.0.0.1:5082",
        changeOrigin: true,
      },
    },
  },
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      "/api": {
        target: process.env.API_URL || "http://127.0.0.1:5080",
        changeOrigin: true,
      },
    },
  },
});
