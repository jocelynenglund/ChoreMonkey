/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_URL: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}

// Build-time injected globals
declare const __BUILD_VERSION__: string;
declare const __BUILD_TIME__: string;
declare const __GIT_SHA__: string;
