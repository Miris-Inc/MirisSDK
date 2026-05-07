# Miris 3D Viewer — Plugin Reference

A FiftyOne plugin that integrates [Miris Spatial Streaming](https://miris.com) into FiftyOne's 3D viewer. Stream full-fidelity 3D assets progressively in the browser alongside your dataset's annotations and metadata.

## Features

- **Progressive streaming** — assets appear at low fidelity within one frame and sharpen to full quality in seconds via Miris SDK
- **Bounding box overlays** — 3D detection annotations from your FiftyOne dataset render as wireframe boxes over the stream
- **Idempotent asset sync** — the `sync_miris_assets` operator fetches your Miris asset library and upserts samples into a dedicated dataset; existing samples are updated, new assets are added
- **Viewer key management** — the viewer key is baked into the bundle at build time; a built-in default is provided so the plugin works out of the box

---

## Prerequisites

- **FiftyOne** >= 1.0 (`pip install fiftyone`)
- **Node.js** >= 18 (to build the JS bundle)
- **Yarn** — `npm install -g yarn`
- **Miris viewer key** — create one at [app.miris.com](https://app.miris.com)
- **HTTPS** — Miris SDK may require a secure context due to loading a WASM module (see [HTTPS setup](#https-setup))

---

## Installation

### 1. Symlink the plugin

```bash
PLUGINS_DIR=$(python -c "import fiftyone as fo; print(fo.config.plugins_dir)")
ln -s "$(pwd)/miris-viewer" "$PLUGINS_DIR/miris-viewer"
```

### 2. Build the JavaScript bundle

```bash
cd miris-viewer/js
yarn
yarn build
```

This produces `js/dist/index.umd.js` — the bundle FiftyOne loads at runtime. The bundle includes Three.js and the Miris SDK (~15 MB unminified).

The build reads `MIRIS_VIEWER_KEY` from the environment and bakes it into the bundle as the default viewer key. If `MIRIS_VIEWER_KEY` is not set, a built-in fallback key is used.

```bash
MIRIS_VIEWER_KEY="your-key" yarn build
```

### 3. (Optional) HTTPS Setup

The Miris SDK WASM module may necessitate HTTPS. FiftyOne's dev server runs on HTTP by default. Use [Caddy](https://caddyserver.com) as a local reverse proxy:

```bash
# macOS
brew install caddy
caddy reverse-proxy --from localhost:5152 --to localhost:5151

# Trust the local CA (one-time)
sudo security add-trusted-cert -d -r trustRoot \
  -k /Library/Keychains/System.keychain \
  ~/Library/Application\ Support/Caddy/pki/authorities/local/root.crt
```

---

## Startup

The plugin requires a **dedicated FiftyOne dataset** as its Miris asset registry. Create it once, then launch the app pointing at it. Each sample in this dataset is an **image sample** whose `filepath` is a locally cached thumbnail of the corresponding Miris asset; the dataset must be persistent so samples survive restarts.

```python
import fiftyone as fo

dataset = fo.Dataset(name="miris-assets", persistent=True)
session = fo.launch_app(dataset, port=5151)
input("Press Enter to stop...")
```

Open `http://localhost:5151` in your browser. If the Miris Viewer panel does not load, switch to `https://localhost:5152` (see [HTTPS Setup](#3-optional-https-setup)).

On subsequent runs, you may load the existing dataset instead of creating a new one:

```python
import fiftyone as fo

dataset = fo.load_dataset("miris-assets")
session = fo.launch_app(dataset, port=5151)
input("Press Enter to stop...")
```

---

## Usage

### Syncing assets

1. Open the dataset in the FiftyOne app.
2. Open the operator browser (press `` ` `` or click the operator icon).
3. Run **Sync Miris Assets**.

Supply your viewer key in the operator input, or leave it blank to use the default baked in at build time. The viewer key must have read access to your Miris asset library.

The operator fetches your asset library from Miris and upserts one sample per asset:

| Field | Description |
|---|---|
| `filepath` | Locally cached thumbnail image (`~/fiftyone/<dataset>/thumbnails/`) |
| `miris_asset_uuid` | Miris asset identifier used to open the stream |
| `miris_asset_name` | Human-readable asset name |
| `miris_thumbnail_url` | Original remote thumbnail URL |

**Sync is idempotent.** Running it again updates existing samples with the latest metadata and adds samples for any new assets. It is safe to re-run as your Miris library grows.

The viewer key used for sync is stored in `dataset.info["miris_viewer_key"]` and reused automatically when the Miris Viewer panel opens samples from that dataset.

### Opening the Miris Viewer

Click any synced sample to open it in the modal. Select **Miris Viewer** from the panel list. The panel streams the 3D asset for any sample that has a `miris_asset_uuid` field.

> **Note:** The Miris Viewer panel only appears in the panel picker for datasets that contain a `miris_asset_uuid` field or have a `miris_viewer_key` stored in `dataset.info`. Running **Sync Miris Assets** on a dataset satisfies both conditions automatically.

---

## Plugin File Structure

```
miris-viewer/
├── fiftyone.yml              # Plugin manifest (name, operators)
├── __init__.py               # Python: UpsertMirisAsset operator
├── README.md
└── js/
    ├── package.json
    ├── vite.config.ts        # UMD build config
    ├── tsconfig.json
    └── src/
        ├── index.tsx                    # Entry: registers panel + operators
        ├── Looker3dClonePanel.tsx       # Panel root — FiftyOne Looker3D with Miris
        ├── syncMirisAssets.ts           # JS operator: sync_miris_assets
        ├── mirisScene.ts                # Miris scene singleton
        ├── stubs/                       # @fiftyone/* build-time stubs
        └── looker-3d/
            └── fo3d/
                └── mesh/
                    └── MirisStream.tsx  # Miris streaming Three.js component
```

---

## Development

### Watch mode

```bash
cd miris-viewer/js
yarn dev
```

Vite rebuilds on file changes. Reload the FiftyOne page to pick up the new bundle.

### Running tests

```bash
cd miris-viewer/js
yarn test
```

### Build notes

| Setting | Reason |
|---|---|
| `minify: false` | Vite's minifier shadows UMD factory parameter names, breaking `registerComponent` |
| `jsxRuntime: "classic"` | FiftyOne exposes `window.React` but not `react/jsx-runtime` |
| `three` aliased to `node_modules/three` | Prevents duplicate Three.js instances between `@miris-inc/three` and R3F |
| `@fiftyone/*` externalized | Mapped to FiftyOne's runtime globals (`__fop__`, `__foo__`, `__fos__`, `__foc__`) |
| `sourcemap: false` | Source maps disabled in production builds |

---

## Troubleshooting

### "Not authorized" in the browser console

The viewer key baked into the bundle does not have permission for this asset.

- Verify the key has access to the asset at [app.miris.com](https://app.miris.com)
- Rebuild with a key that has the correct permissions: `MIRIS_VIEWER_KEY="your-key" yarn build`

### Miris stream is black or empty

- Confirm you're on HTTPS (`https://localhost:5152`, not `http://`)
- Check the browser console for Miris SDK initialization errors
- Verify `yarn list --pattern three` shows a single Three.js instance

### Panel shows "Unsupported view"

The JS bundle failed to register. Check the browser console. Common causes:

- `process is not defined` — rebuild with the `define` config in `vite.config.ts`
- `Cannot read properties of undefined (reading 'useMemo')` — React globals mapping is wrong

### Samples are missing after restart

FiftyOne datasets are ephemeral by default. The sync operator sets `dataset.persistent = True` automatically, but verify with:

```python
import fiftyone as fo
ds = fo.load_dataset("your-dataset")
print(ds.persistent)  # should be True
```

---

## Requirements

| Component | Version |
|---|---|
| FiftyOne | >= 1.0 |
| Node.js | >= 18 |
| Three.js | >= r179 (bundled at r184) |
| `@miris-inc/three` | latest (bundled) |
| Browser | Chrome 90+, Firefox 88+, Safari 14+ |
| HTTPS | Required for Miris streaming |

## License

Apache-2.0
