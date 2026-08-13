# Miris SDK

The Miris SDK streams Miris assets directly into any Unity scene. Drop a Miris Stream prefab into your scene, point it at an asset, and players can view and interact with it right there — the Miris SDK handles the network streaming, decoding, and rendering.

## 1. Overview

This document covers installing the Miris Unity SDK and getting your first asset streaming in a scene. For the Web SDK, REST API, and CLI, see the [Miris documentation](https://docs.miris.com).

## 2. Prerequisites

This document assumes familiarity with the Unity Editor and C#.

* Unity 6000.0.58f2 or newer.
* An asset ID from Miris's asset upload service (of the form `aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee`).
* Full platform and OS requirements are listed in the [Unity Integration guide](Unity/README.md#requirements).

## 3. Installing the Miris Unity SDK

1. Open your Unity project and go to **Window → Package Manager**.
2. Use the **+** button to **Install package from git URL...** and paste:

   ```
   https://github.com/Miris-Inc/MirisSDK.git?path=Unity#latest
   ```

3. The SDK needs native libraries downloaded into `Assets/Plugins/Miris`. If they aren't already present, use **Tools → Miris → Platform Downloader** in the Editor to fetch them.
4. If your project uses URP, add the `Gaussian Splat Render Pass` render feature to your renderer assets.

Full step-by-step instructions, including screenshots and platform-specific graphics API notes, are in the [Unity Integration guide](Unity/README.md#installation).

## 4. Usage Instructions

1. Drag the `Miris Stream` and `Miris Stream Controller` prefabs into your scene.
   * `Miris Stream Controller` manages the connection to the Miris service and drives streaming for every `Miris Stream` in the scene. Only one should exist per scene.
   * `Miris Stream` represents a single streamed asset.
2. On the `Miris Stream` prefab, set the asset ID for the content you want to stream.
3. Press play, or view the stream directly in the editor — no play mode required.

## 5. Troubleshooting

If the Platform Downloader can't find a release, or streaming content doesn't appear, see the **Prefab setup** and **Notes** sections of the [Unity Integration guide](Unity/README.md). For anything else, [contact Miris support](https://docs.miris.com).

## 6. Copyright and Licensing Information

Apache License, Version 2.0. Full details are available in the [LICENSE](LICENSE) file.
