import { useCallback, useEffect, useRef, useState } from "react"
import { PublicApiError } from "@/api/publicBrowsingApi"

export type PublicResourceState<T> =
  | { status: "loading" }
  | { status: "success"; data: T }
  | { status: "error"; error: PublicApiError }

export function usePublicApiResource<T>(
  requestKey: string,
  load: (signal: AbortSignal) => Promise<T>,
) {
  const loadRef = useRef(load)
  const requestNumber = useRef(0)
  const [retryNumber, setRetryNumber] = useState(0)
  const [state, setState] = useState<PublicResourceState<T>>({
    status: "loading",
  })

  loadRef.current = load

  useEffect(() => {
    const controller = new AbortController()
    const currentRequest = ++requestNumber.current
    setState({ status: "loading" })

    Promise.resolve()
      .then(() => loadRef.current(controller.signal))
      .then((data) => {
        if (
          !controller.signal.aborted &&
          currentRequest === requestNumber.current
        ) {
          setState({ status: "success", data })
        }
      })
      .catch((error: unknown) => {
        if (
          controller.signal.aborted ||
          currentRequest !== requestNumber.current
        )
          return
        setState({
          status: "error",
          error:
            error instanceof PublicApiError
              ? error
              : new PublicApiError(
                  "unexpected",
                  "Đã xảy ra lỗi khi tải dữ liệu.",
                ),
        })
      })

    return () => controller.abort()
  }, [requestKey, retryNumber])

  const retry = useCallback(() => setRetryNumber((current) => current + 1), [])
  return { ...state, retry }
}
