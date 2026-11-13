import { useEffect, useRef, useState } from "react"
import { googleClientId, loadGoogleIdentity } from "@/auth/googleIdentity"

type Props = {
  /** Google's own label on the official button. */
  text: "signin_with" | "signup_with"
  onCredential: (credential: string) => void
  onError: (message: string) => void
}

/**
 * Official "Sign in with Google" button. Renders nothing when VITE_GOOGLE_CLIENT_ID is not set,
 * so builds without Google configuration look exactly as before.
 */
export default function GoogleSignInButton({ text, onCredential, onError }: Props) {
  const clientId = googleClientId()
  const container = useRef<HTMLDivElement>(null)
  const [unavailable, setUnavailable] = useState(false)
  // Keep the latest callbacks without re-initializing Google on every render.
  const handlers = useRef({ onCredential, onError })
  handlers.current = { onCredential, onError }

  useEffect(() => {
    if (!clientId) return
    let active = true
    loadGoogleIdentity()
      .then((google) => {
        if (!active || !container.current) return
        google.initialize({
          client_id: clientId,
          ux_mode: "popup",
          callback: (response) => {
            if (response.credential) handlers.current.onCredential(response.credential)
            else handlers.current.onError("Đăng nhập Google không thành công. Vui lòng thử lại.")
          },
        })
        google.renderButton(container.current, {
          type: "standard",
          theme: "outline",
          size: "large",
          shape: "rectangular",
          text,
          width: container.current.clientWidth || 366,
          locale: "vi",
        })
      })
      .catch(() => {
        if (active) setUnavailable(true)
      })
    return () => {
      active = false
    }
  }, [clientId, text])

  if (!clientId) return null

  return (
    <div>
      <div className="my-5 flex items-center gap-3 text-[11px] font-semibold uppercase tracking-[.14em] text-slate-400">
        <span className="h-px flex-1 bg-slate-200" />
        hoặc
        <span className="h-px flex-1 bg-slate-200" />
      </div>
      <div ref={container} data-testid="google-signin-button" className="flex min-h-[44px] justify-center" />
      {unavailable && (
        <p className="mt-2 text-center text-[12px] text-slate-500">
          Đăng nhập bằng Google tạm thời không khả dụng.
        </p>
      )}
    </div>
  )
}
