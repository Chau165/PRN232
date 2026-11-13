import { useEffect, useState } from "react"
import { ApiError } from "@/api/apiClient"
import { getPaymentRequest } from "@/services/invoicePaymentService"
import { invoicePaymentError, paymentIsFinal } from "@/utils/invoicePayment"
import type { PaymentRequestStatusDto } from "@/types/invoicePayment"

export const PAYMENT_POLL_MS = 4000
export const PAYMENT_POLL_LIMIT = 30

export function useInvoicePaymentStatus(accessToken: string | null, requestId: number | null) {
  const [data, setData] = useState<PaymentRequestStatusDto | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [checking, setChecking] = useState(false)
  const [paused, setPaused] = useState(false)
  const [attempt, setAttempt] = useState(0)
  useEffect(() => {
    setData(null)
    setError(null)
    setPaused(false)
    if (!accessToken || !requestId) return
    const controller = new AbortController()
    let timer: ReturnType<typeof setTimeout> | undefined
    let count = 0
    const deadline = Date.now() + 120000
    const check = async () => {
      setChecking(true)
      let stop = false
      const request = new AbortController()
      const abort = () => request.abort()
      controller.signal.addEventListener("abort", abort)
      const timeout = setTimeout(abort, 15000)
      try {
        const result = await getPaymentRequest(accessToken, requestId, request.signal)
        if (controller.signal.aborted) return
        // Never carry over data from a different request, even after navigation or a late response.
        if (result.paymentRequestId !== requestId) throw new Error("Payment request mismatch")
        setData(result)
        setError(null)
        stop = paymentIsFinal(result)
      } catch (failure) {
        if (controller.signal.aborted) return
        setError(invoicePaymentError(failure))
        stop = failure instanceof ApiError && [401, 403, 404, 429].includes(failure.status ?? 0)
      } finally {
        clearTimeout(timeout)
        controller.signal.removeEventListener("abort", abort)
        if (!controller.signal.aborted) setChecking(false)
      }
      if (controller.signal.aborted) return
      count++
      if (!stop && count < PAYMENT_POLL_LIMIT && Date.now() < deadline) timer = setTimeout(check, PAYMENT_POLL_MS)
      else if (!stop) setPaused(true)
    }
    void check()
    return () => { controller.abort(); clearTimeout(timer) }
  }, [accessToken, requestId, attempt])
  return { data: data?.paymentRequestId === requestId ? data : null, error, checking, paused, refresh: () => setAttempt(value => value + 1) }
}
