// Google Identity Services ("Sign in with Google") loader. Only the parts the app uses are typed.

export interface GoogleCredentialResponse {
  credential?: string
}

interface GoogleButtonOptions {
  type?: "standard" | "icon"
  theme?: "outline" | "filled_blue" | "filled_black"
  size?: "large" | "medium" | "small"
  text?: "signin_with" | "signup_with" | "continue_with" | "signin"
  shape?: "rectangular" | "pill" | "circle" | "square"
  width?: number
  locale?: string
}

export interface GoogleAccountsId {
  initialize(options: {
    client_id: string
    callback: (response: GoogleCredentialResponse) => void
    ux_mode?: "popup" | "redirect"
  }): void
  renderButton(parent: HTMLElement, options: GoogleButtonOptions): void
}

declare global {
  interface Window {
    google?: { accounts?: { id?: GoogleAccountsId } }
  }
}

export const GOOGLE_IDENTITY_SCRIPT_URL = "https://accounts.google.com/gsi/client"

/** The OAuth client id, or null when Google sign-in is not configured for this build. */
export function googleClientId(): string | null {
  const value = import.meta.env.VITE_GOOGLE_CLIENT_ID
  return typeof value === "string" && value.trim() ? value.trim() : null
}

let loading: Promise<GoogleAccountsId> | null = null

/** Loads the Google Identity Services script once and resolves with google.accounts.id. */
export function loadGoogleIdentity(): Promise<GoogleAccountsId> {
  const existing = window.google?.accounts?.id
  if (existing) return Promise.resolve(existing)
  if (loading) return loading

  loading = new Promise<GoogleAccountsId>((resolve, reject) => {
    const script = document.createElement("script")
    script.src = GOOGLE_IDENTITY_SCRIPT_URL
    script.async = true
    script.defer = true
    script.onload = () => {
      const api = window.google?.accounts?.id
      if (api) resolve(api)
      else reject(new Error("Google Identity Services did not load."))
    }
    script.onerror = () => reject(new Error("Không tải được dịch vụ đăng nhập Google."))
    document.head.appendChild(script)
  }).catch((error: unknown) => {
    loading = null
    throw error
  })
  return loading
}
