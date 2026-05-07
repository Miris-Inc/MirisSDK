// Stubs for @fiftyone/annotation used in looker-3d annotation module
export const useAnnotationEventBus = () => ({
  emit: (_event: string, _data?: any) => {},
  on: (_event: string, _handler: any) => () => {},
});

export const useAnnotationEventHandler = (
  _event: string,
  _handler: any
) => {};
