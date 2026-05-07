import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const STUBS = path.resolve(__dirname, "src/stubs");

// FiftyOne exposes these as globals in the App runtime
const FIFTYONE_GLOBALS: Record<string, string> = {
  react: "React",
  "react-dom": "ReactDOM",
  recoil: "recoil",
  "@fiftyone/plugins": "__fop__",
  "@fiftyone/operators": "__foo__",
  "@fiftyone/spaces": "__fos__",
  "@fiftyone/state": "__fos__",
  "@fiftyone/components": "__foc__",
};

const DEFAULT_VIEWER_KEY = "4YIGMPUj5-fL8n0jkp1kQpJktss_UaBDMW9jwJb08f4";
const viewerKey = process.env.MIRIS_VIEWER_KEY ?? DEFAULT_VIEWER_KEY;

export default defineConfig({
  resolve: {
    alias: [
      // Deduplicate three so @miris-inc/three and R3F share the same instance
      { find: "three", replacement: path.resolve(__dirname, "node_modules/three") },

      // ── @fiftyone/* stubs (not available as globals in FiftyOne runtime) ────

      { find: /^@fiftyone\/utilities(\/.*)?$/, replacement: path.resolve(STUBS, "fiftyone-utilities.ts") },
      { find: /^@fiftyone\/looker(\/.*)?$/, replacement: path.resolve(STUBS, "fiftyone-looker.ts") },
      { find: /^@fiftyone\/core(\/.*)?$/, replacement: path.resolve(STUBS, "fiftyone-core.ts") },
      { find: /^@fiftyone\/annotation(\/.*)?$/, replacement: path.resolve(STUBS, "fiftyone-annotation.ts") },
      { find: /^@fiftyone\/commands(\/.*)?$/, replacement: path.resolve(STUBS, "fiftyone-commands.ts") },
      { find: /^@fiftyone\/components\/.+$/, replacement: path.resolve(STUBS, "fiftyone-core.ts") },
      { find: "@fiftyone/state/src/recoil/customEffects", replacement: path.resolve(STUBS, "fiftyone-state-custom-effects.ts") },
      { find: /^@fiftyone\/state\/src\/jotai(\/.*)?$/, replacement: path.resolve(STUBS, "fiftyone-state-jotai.ts") },
      { find: /^@fiftyone\/state\/src\/hooks\/(.*)/, replacement: path.resolve(STUBS, "fiftyone-state-hooks.ts") },
      { find: /^@fiftyone\/looker-3d(\/.*)?$/, replacement: path.resolve(STUBS, "fiftyone-looker-3d.ts") },
      { find: /\/utilities\/src\/paths$/, replacement: path.resolve(STUBS, "utilities-paths.ts") },
    ],
  },
  plugins: [
    react({
      // Use classic JSX transform (React.createElement) — FiftyOne exposes
      // React as window.React but does not expose react/jsx-runtime separately.
      jsxRuntime: "classic",
    }),
  ],
  define: {
    "process.env.NODE_ENV": JSON.stringify("production"),
    "process.env": JSON.stringify({}),
    "import.meta.env.VITE_VIEWER_KEY": JSON.stringify(viewerKey),
  },
  build: {
    lib: {
      entry: "src/index.tsx",
      formats: ["umd"],
      name: "MirisViewer",
      fileName: () => "index.umd.js",
    },
    outDir: "dist",
    sourcemap: false,
    // Minification disabled: the minifier reuses the UMD factory parameter
    // names as arrow-function params, shadowing the externalised imports and
    // causing registerComponent to target undefined instead of __fop__.
    minify: false,
    rollupOptions: {
      external: Object.keys(FIFTYONE_GLOBALS),
      output: {
        globals: FIFTYONE_GLOBALS,
      },
    },
  },
});
