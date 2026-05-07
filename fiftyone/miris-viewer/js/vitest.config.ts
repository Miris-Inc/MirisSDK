/**
 * Vitest config: separate from the Vite build config because
 * @voxel51/fiftyone-js-plugin-build is for the UMD build, not tests.
 */
import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  test: {
    environment: "jsdom",
    globals: true,
    include: ["src/**/*.test.ts", "src/**/*.test.tsx"],
  },
});
