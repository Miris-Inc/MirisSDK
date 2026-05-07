import { registerComponent, PluginComponentType } from "@fiftyone/plugins";
import { registerOperator } from "@fiftyone/operators";
import { Miris } from "@miris-inc/three";
import Looker3dClonePanel from "./Looker3dClonePanel";
import { initMirisScene } from "./mirisScene";
import { SyncMirisAssets, DEFAULT_VIEWER_KEY } from "./syncMirisAssets";

// Pass the promise directly — initMirisScene stores it internally so
// getMirisScene() can be awaited later without a top-level await here
// (top-level await is incompatible with the UMD build format).
initMirisScene(Miris.instance(), DEFAULT_VIEWER_KEY);

registerOperator(SyncMirisAssets, "@miris/viewer");
registerComponent({
  name: "Miris Viewer",
  label: "Miris Viewer",
  component: Looker3dClonePanel,
  type: PluginComponentType.Panel,
  activator: activateOnlyOnMirisSample,
  panelOptions: {
    surfaces: "modal",
  },
});


function activateOnlyOnMirisSample(ctx: any) {
  const dataset = ctx?.dataset;
  if (!dataset) return false;
  const fields: any[] = Array.isArray(dataset.sampleFields) ? dataset.sampleFields : [];
  const hasMirisField = fields.some(
    (f: any) => f?.name === "miris_asset_uuid" || f?.path === "miris_asset_uuid",
  );
  const hasViewerKey = !!dataset.info?.miris_viewer_key;
  return hasMirisField || hasViewerKey;
}
