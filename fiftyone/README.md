# Miris 3D Viewer for FiftyOne

Stream full-fidelity 3D assets inside [FiftyOne](https://voxel51.com) using [Miris Spatial Streaming](https://miris.com). Browse, navigate, and label 3D scenes progressively in the browser — no downloads, no desktop tools.

## How It Works

The plugin uses a **dedicated FiftyOne dataset** as its asset registry. Each sample in this dataset represents one Miris asset — its `filepath` is a locally cached thumbnail image, and Miris-specific metadata fields identify the asset for streaming.

Assets are populated via the **Sync Miris Assets** operator. Sync can be run at any time: existing asset samples are updated in place, and newly discovered assets are added as new samples. A viewer key is required to authenticate with Miris during sync; if not provided, the plugin falls back to a built-in default.

Once synced, opening any sample in the **Miris Viewer** panel streams the corresponding 3D asset directly from Miris.

- **Progressive streaming** via Miris SDK
- **Live bounding box overlays** from your FiftyOne dataset labels
- **Idempotent asset sync** — safe to run repeatedly as your Miris library grows
- Built on top of FiftyOne's native Looker3D viewer (React Three Fiber + Three.js)

## Repository Structure

```
.
├── README.md
└── miris-viewer/          # The FiftyOne plugin
    ├── fiftyone.yml       # Plugin manifest
    ├── __init__.py        # Python operators
    ├── README.md          # Installation, usage, and development guide
    └── js/                # TypeScript source + Vite build
```

## Getting Started

See **[miris-viewer/README.md](miris-viewer/README.md)** for installation, configuration, and usage.

## License

Apache-2.0
