# Miris SDK

The Miris SDK streams Miris assets directly into any Unity scene. Drop a Miris Stream prefab into your scene, point it at an asset, and players can view and interact with it right there — the Miris SDK handles the network streaming, decoding, and rendering.

## Platforms

* **Unity** — see the [Unity Integration guide](Unity/README.md) for requirements, installation, and setup.
* **Web** — this repo doesn't contain the Web SDK; see the [Web SDK docs](https://docs.miris.com/web-sdk) if you're building for the browser.

## Unity quickstart

1. Install the package via the Unity Package Manager. See [Unity/README.md](Unity/README.md#installation) for full instructions, including downloading the required native libraries.
2. Drop the `Miris Stream` and `Miris Stream Controller` prefabs into your scene.
   * `Miris Stream Controller` manages the connection to the Miris service and drives streaming for every `Miris Stream` in the scene. Only one should exist per scene.
   * `Miris Stream` represents a single streamed asset. Set its asset ID (supplied by Miris's asset upload service, in the form `aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee`) to start streaming.
3. Press play, or view the stream directly in the editor.