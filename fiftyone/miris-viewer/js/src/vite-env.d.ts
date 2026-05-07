/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_VIEWER_KEY: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
