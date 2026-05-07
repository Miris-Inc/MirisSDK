import React from "react";
import { Looker3d } from "./looker-3d/Looker3d";

const Looker3dClonePanel = () => {
  return (
    <div style={{ width: "100%", height: "100%", position: "relative" }}>
      <Looker3d />
    </div>
  );
};

export default Looker3dClonePanel;
