# Getting Started

A minimal scene that streams one Miris asset.

## Contents

- `GettingStarted.unity` — a `Main Camera`, a `Directional Light`, and one `Miris Stream`.

## Usage

1. Set your viewer key in **Miris → Settings**.
2. Open `GettingStarted.unity`.
3. Choose the asset to stream, either way:
   - **Asset ID:** select `Miris Stream` and paste your asset's ID into **Asset Id**.
   - **Asset Browser:** open **Miris → Asset Browser**. Double-click an asset, or drag it into the Hierarchy or Scene view, to add a new `Miris Stream` already set up for it. You can then delete the original `Miris Stream`.
4. Press Play, or view the stream directly in the Scene view.

## Finding your asset

Assets can be very large, or authored away from the origin, so the asset may not be in view of the `Main Camera` at first. If you don't see it, select `Miris Stream` and press **F** in the Scene view to frame it, then move the `Main Camera` or the `Miris Stream` to bring it into view.
