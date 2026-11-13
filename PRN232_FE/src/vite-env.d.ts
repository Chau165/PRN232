interface ImportMetaEnv {
  readonly VITE_API_BASE_URL?: string
  readonly VITE_REVERSE_GEOCODING_URL?: string
  readonly VITE_GEOCODING_URL?: string
  readonly VITE_PUBLIC_MAP_TILE_URL?: string
  readonly VITE_PUBLIC_MAP_ATTRIBUTION?: string
  readonly VITE_GOOGLE_CLIENT_ID?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
