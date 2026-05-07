declare module "recoil" {
  export function useRecoilValue<T>(recoilValue: unknown): T;
}

/**
 * Type declarations for @fiftyone/* packages.
 *
 * These packages are not published to npm. They are provided at runtime
 * by the FiftyOne App. These stubs let TypeScript compile the plugin
 * during development without the full FiftyOne monorepo.
 */

declare module "@fiftyone/plugins" {
  export enum PluginComponentType {
    Component = "Component",
    Panel = "Panel",
    Visualizer = "Visualizer",
  }

  export interface ActivatorContext {
    dataset?: {
      name: string;
      info: Record<string, unknown>;
      sampleFields: Array<{ name: string; path?: string; [key: string]: unknown }>;
      [key: string]: unknown;
    };
    [key: string]: unknown;
  }

  export interface RegisterComponentOptions {
    name: string;
    label: string;
    component: React.ComponentType<any>;
    type: PluginComponentType;
    activator: (ctx?: ActivatorContext) => boolean;
    Icon?: React.ComponentType;
    panelOptions?: { surfaces?: string; [key: string]: unknown };
  }

  export function registerComponent(options: RegisterComponentOptions): void;
}

declare module "@fiftyone/operators" {
  export function useOperatorExecutor(
    operatorName: string,
  ): {
    execute: (params?: Record<string, unknown>) => Promise<unknown>;
    isLoading: boolean;
  };

  export class OperatorConfig {
    constructor(options: {
      name: string;
      label: string;
      unlisted?: boolean;
      dynamic?: boolean;
    });
  }

  export interface ExecutionContext {
    params: Record<string, unknown>;
    dataset: unknown;
  }

  export function executeOperator(
    uri: string,
    params?: Record<string, unknown>,
  ): Promise<{ result?: Record<string, unknown> } | undefined>;

  export namespace types {
    class Object {
      str(name: string, options?: {
        label?: string;
        description?: string;
        placeholder?: string;
        required?: boolean;
        default?: string;
      }): void;
    }
    class Property {
      constructor(type: types.Object);
    }
  }

  export abstract class Operator {
    abstract get config(): OperatorConfig;
    resolveInput(ctx: ExecutionContext): types.Property | void;
    execute(ctx: ExecutionContext): Promise<void> | void;
  }

  export function registerOperator(
    operator: typeof Operator,
    pluginName: string,
  ): void;
}

declare module "@fiftyone/spaces" {
  export function usePanelState<T = unknown>(
    defaultValue?: T,
  ): [T, (value: T) => void];

  export function usePanelId(): string;
}

declare module "@fiftyone/state" {
  interface DatasetInfo {
    miris_viewer_key?: string;
    [key: string]: unknown;
  }
  interface Dataset {
    name: string;
    info: DatasetInfo;
    sampleFields: Record<string, unknown>[];
    [key: string]: unknown;
  }
  export const dataset: unknown; // Recoil RecoilState<Dataset | null>
  export const selectedSamples: unknown;
}
