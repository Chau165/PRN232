import { defineConfig } from "vitest/config"
import { fileURLToPath } from "node:url"

export default defineConfig({
  resolve: { alias: { "@": fileURLToPath(new URL("./src", import.meta.url)) } },
  test: { environment: "jsdom", include: ["tests/e2e/**/*.e2e.tsx"], setupFiles: ["./tests/setup.ts"], testTimeout: 60000 },
})
