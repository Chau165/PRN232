import { useEffect, useState } from "react"
import { useAuth } from "@/auth/AuthContext"
import { ApiError } from "@/api/apiClient"
import { getBookingRefunds } from "@/services/bookingRefundService"
import type { BookingRefundDto } from "@/types/bookingRefund"
import { refundError, refundIsActive } from "@/utils/bookingRefund"

export function useBookingRefundHistory(bookingId: number) {
  const { accessToken } = useAuth()
  const key = `${accessToken}:${bookingId}`
  const [state, setState] = useState<{
    key: string
    data: BookingRefundDto[] | null
    error: string | null
    paused: boolean
  }>({ key, data: null, error: null, paused: false })
  const [revision, setRevision] = useState(0)
  useEffect(() => {
    setState({ key, data: null, error: null, paused: false })
    if (!accessToken) return
    const controller = new AbortController()
    let timer: ReturnType<typeof setTimeout> | undefined
    let count = 0
    const deadline = Date.now() + 300000
    const check = async () => {
      const request = new AbortController()
      const abort = () => request.abort()
      controller.signal.addEventListener("abort", abort)
      const timeout = setTimeout(abort, 15000)
      try {
        const history = await getBookingRefunds(
          accessToken,
          bookingId,
          request.signal,
        )
        if (controller.signal.aborted) return
        if (
          !Array.isArray(history) ||
          history.some((item) => item.bookingId !== bookingId)
        )
          throw new Error("Refund scope mismatch")
        count++
        const active = refundIsActive(history[0]?.status)
        const paused = active && (count >= 20 || Date.now() >= deadline)
        setState({ key, data: history, error: null, paused })
        if (active && !paused) timer = setTimeout(check, 15000)
      } catch (error) {
        if (controller.signal.aborted) return
        // Clear old data on permission loss; never retain a previous customer's/history's result.
        const denied =
          error instanceof ApiError &&
          [401, 403, 404].includes(error.status ?? 0)
        setState((previous) => ({
          key,
          data: denied ? null : previous.key === key ? previous.data : null,
          error: refundError(error),
          paused: true,
        }))
      } finally {
        clearTimeout(timeout)
        controller.signal.removeEventListener("abort", abort)
      }
    }
    void check()
    return () => {
      controller.abort()
      clearTimeout(timer)
    }
  }, [accessToken, bookingId, key, revision])
  const current =
    state.key === key ? state : { data: null, error: null, paused: false }
  return {
    ...current,
    loading: current.data === null && !current.error,
    refresh: () => setRevision((value) => value + 1),
  }
}
