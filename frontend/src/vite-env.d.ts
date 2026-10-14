/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** API origin for native builds, e.g. https://opentablo.example.com. Empty on the web. */
  readonly VITE_API_BASE?: string
}
