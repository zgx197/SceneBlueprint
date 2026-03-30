import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

function createManualChunks(id: string) {
  if (id.includes("node_modules")) {
    if (
      id.includes("@react-three/drei") ||
      id.includes("@react-three/fiber") ||
      id.includes("three")
    ) {
      return "scene-3d-vendor";
    }

    if (id.includes("react") || id.includes("scheduler")) {
      return "react-vendor";
    }

    if (id.includes("@tauri-apps")) {
      return "tauri-vendor";
    }
  }

  return undefined;
}

export default defineConfig({
  plugins: [react()],
  server: {
    host: "0.0.0.0",
    port: 1420,
    strictPort: true,
  },
  build: {
    rollupOptions: {
      output: {
        manualChunks: createManualChunks,
      },
    },
  },
  clearScreen: false,
});
