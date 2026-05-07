import { createContext, useContext, useState } from "react";
import type { OverlayLabel } from "../types";

interface MirisLiveLabelsContextT {
  liveDetections: OverlayLabel[];
  setLiveDetections: (labels: OverlayLabel[]) => void;
}

const MirisLiveLabelsContext = createContext<MirisLiveLabelsContextT>({
  liveDetections: [],
  setLiveDetections: () => {},
});

export const useMirisLiveLabels = () => useContext(MirisLiveLabelsContext);

export const MirisLiveLabelsProvider = ({
  children,
}: {
  children: React.ReactNode;
}) => {
  const [liveDetections, setLiveDetections] = useState<OverlayLabel[]>([]);
  return (
    <MirisLiveLabelsContext.Provider value={{ liveDetections, setLiveDetections }}>
      {children}
    </MirisLiveLabelsContext.Provider>
  );
};
