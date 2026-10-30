import { useCallback, useEffect, useRef, useState } from "react"
import { getNotifications, markNotificationRead } from "@/services/notificationService"
import type { NotificationDto } from "@/types/notification"

export interface NotificationToast {
  notification: NotificationDto
  key: number
}

export function useCustomerNotifications(accessToken: string | null, enabled: boolean) {
  const [notifications, setNotifications] = useState<NotificationDto[]>([])
  const [toasts, setToasts] = useState<NotificationToast[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const requestVersion = useRef(0)
  const tokenRef = useRef(accessToken)
  const baselineToken = useRef<string | null>(null)
  const seenIds = useRef(new Set<number>())
  const [ownerToken, setOwnerToken] = useState<string | null>(null)
  tokenRef.current = accessToken

  const refresh = useCallback(async (signal?: AbortSignal) => {
    if (!accessToken || !enabled) return
    const version = ++requestVersion.current
    setLoading(true)
    setError(null)
    try {
      const allItems = await getNotifications(accessToken, signal)
      const unreadItems = await getNotifications(accessToken, signal, false)
      if (signal?.aborted || version !== requestVersion.current || tokenRef.current !== accessToken) return
      const isBaseline = baselineToken.current !== accessToken
      const incoming = unreadItems.filter((item) => !seenIds.current.has(item.notificationId))
      for (const item of allItems) seenIds.current.add(item.notificationId)
      for (const item of unreadItems) seenIds.current.add(item.notificationId)
      baselineToken.current = accessToken
      setError(null)
      setOwnerToken(accessToken)
      setNotifications(allItems)
      if (!isBaseline && incoming.length > 0) {
        setToasts((current) => [
          ...incoming.map((notification) => ({ notification, key: notification.notificationId })),
          ...current,
        ].slice(0, 5))
      }
    } catch (cause) {
      if (version === requestVersion.current && tokenRef.current === accessToken) {
        setOwnerToken(accessToken)
        setError(cause instanceof Error ? cause.message : "Không thể tải thông báo.")
      }
    } finally {
      if (version === requestVersion.current && tokenRef.current === accessToken) setLoading(false)
    }
  }, [accessToken, enabled])

  useEffect(() => {
    requestVersion.current++
    baselineToken.current = null
    seenIds.current.clear()
    setOwnerToken(null)
    setNotifications([])
    setToasts([])
    setError(null)
    setLoading(false)
    if (!accessToken || !enabled) return

    const controller = new AbortController()
    void refresh(controller.signal)
    const refreshWhenActive = () => {
      if (document.visibilityState === "visible") void refresh()
    }
    const timer = window.setInterval(refreshWhenActive, 15_000)
    window.addEventListener("focus", refreshWhenActive)
    document.addEventListener("visibilitychange", refreshWhenActive)
    return () => {
      controller.abort()
      requestVersion.current++
      window.clearInterval(timer)
      window.removeEventListener("focus", refreshWhenActive)
      document.removeEventListener("visibilitychange", refreshWhenActive)
    }
  }, [accessToken, enabled, refresh])

  const markRead = useCallback(async (notificationId: number) => {
    if (!accessToken || !enabled) return
    const item = notifications.find((notification) => notification.notificationId === notificationId)
    if (!item || item.isRead) return
    await markNotificationRead(accessToken, notificationId)
    if (tokenRef.current === accessToken) {
      requestVersion.current++
      setLoading(false)
      setError(null)
      setNotifications((current) => current.map((notification) =>
        notification.notificationId === notificationId ? { ...notification, isRead: true } : notification,
      ))
    }
  }, [accessToken, enabled, notifications])

  const dismissToast = useCallback((notificationId: number) => {
    setToasts((current) => current.filter((toast) => toast.notification.notificationId !== notificationId))
  }, [])

  const currentNotifications = enabled && ownerToken === accessToken ? notifications : []
  const currentToasts = enabled && ownerToken === accessToken ? toasts : []
  return {
    notifications: currentNotifications,
    toasts: currentToasts,
    unreadCount: currentNotifications.filter((item) => !item.isRead).length,
    loading: (enabled && ownerToken !== accessToken) || loading,
    error,
    refresh,
    markRead,
    dismissToast,
  }
}
