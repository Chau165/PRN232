import { useCallback, useEffect, useRef, useState } from "react"
import { HubConnectionBuilder, HubConnectionState, type HubConnection } from "@microsoft/signalr"
import { getNotifications, markNotificationRead, notificationHubUrl } from "@/services/notificationService"
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
  const [connected, setConnected] = useState(false)
  const requestVersion = useRef(0)
  const tokenRef = useRef(accessToken)
  const baselineToken = useRef<string | null>(null)
  const knownIds = useRef(new Set<number>())
  const toastedIds = useRef(new Set<number>())
  const [ownerToken, setOwnerToken] = useState<string | null>(null)
  tokenRef.current = accessToken

  const refresh = useCallback(async (signal?: AbortSignal) => {
    if (!accessToken || !enabled) return
    const version = ++requestVersion.current
    setLoading(true)
    setError(null)
    try {
      const allItems = await getNotifications(accessToken, signal)
      if (signal?.aborted || version !== requestVersion.current || tokenRef.current !== accessToken) return
      const isBaseline = baselineToken.current !== accessToken
      const incoming = isBaseline ? [] : allItems.filter((item) => !item.isRead && !knownIds.current.has(item.notificationId))
      for (const item of allItems) knownIds.current.add(item.notificationId)
      const newToasts = incoming.filter((item) => !toastedIds.current.has(item.notificationId))
      for (const item of newToasts) toastedIds.current.add(item.notificationId)
      baselineToken.current = accessToken
      setError(null)
      setOwnerToken(accessToken)
      setNotifications((current) => {
        const merged = new Map(current.map((item) => [item.notificationId, item]))
        for (const item of allItems) {
          const existing = merged.get(item.notificationId)
          merged.set(item.notificationId, existing ? { ...item, isRead: existing.isRead || item.isRead } : item)
        }
        return [...merged.values()].sort((left, right) => right.notificationId - left.notificationId)
      })
      if (!isBaseline && newToasts.length > 0) {
        setToasts((current) => [
          ...newToasts.map((notification) => ({ notification, key: notification.notificationId })),
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
    knownIds.current.clear()
    toastedIds.current.clear()
    setOwnerToken(null)
    setNotifications([])
    setToasts([])
    setError(null)
    setLoading(false)
    setConnected(false)
    if (!accessToken || !enabled) return

    const controller = new AbortController()
    let disposed = false
    let connection: HubConnection | null = null
    try {
      connection = new HubConnectionBuilder()
        .withUrl(notificationHubUrl(), { accessTokenFactory: () => accessToken })
        .withAutomaticReconnect()
        .build()
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Không thể kết nối thông báo realtime.")
    }

    if (!connection) {
      void refresh(controller.signal)
      const refreshWhenActive = () => { if (document.visibilityState === "visible") void refresh() }
      window.addEventListener("focus", refreshWhenActive)
      document.addEventListener("visibilitychange", refreshWhenActive)
      const retryTimer = window.setInterval(() => {
        if (document.visibilityState === "visible") {
          void refresh()
          setConnected(false)
        }
      }, 30_000)
      return () => {
        controller.abort()
        requestVersion.current++
        window.clearInterval(retryTimer)
        window.removeEventListener("focus", refreshWhenActive)
        document.removeEventListener("visibilitychange", refreshWhenActive)
      }
    }

    connection.on("NotificationCreated", (notification: NotificationDto) => {
      if (disposed || !Number.isSafeInteger(notification?.notificationId) || notification.notificationId < 1 ||
        typeof notification.title !== "string" || typeof notification.type !== "string") return
      knownIds.current.add(notification.notificationId)
      setOwnerToken(accessToken)
      setNotifications((current) => {
        const existing = current.find((item) => item.notificationId === notification.notificationId)
        const merged = new Map(current.map((item) => [item.notificationId, item]))
        merged.set(notification.notificationId, { ...existing, ...notification })
        return [...merged.values()].sort((left, right) => right.notificationId - left.notificationId)
      })
      if (!notification.isRead && !toastedIds.current.has(notification.notificationId)) {
        toastedIds.current.add(notification.notificationId)
        setToasts((current) => current.some((toast) => toast.notification.notificationId === notification.notificationId)
          ? current
          : [{ notification, key: notification.notificationId }, ...current].slice(0, 5))
      }
    })
    connection.on("NotificationReadUpdated", (update: { notificationId: number; isRead: boolean }) => {
      if (disposed || !Number.isSafeInteger(update?.notificationId) || typeof update.isRead !== "boolean") return
      setNotifications((current) => current.map((item) => item.notificationId === update.notificationId
        ? { ...item, isRead: update.isRead }
        : item))
      if (update.isRead) {
        setToasts((current) => current.filter((toast) => toast.notification.notificationId !== update.notificationId))
      }
    })
    connection.onreconnecting(() => setConnected(false))
    connection.onreconnected(() => {
      setConnected(true)
      void refresh()
    })
    connection.onclose(() => setConnected(false))
    void connection.start().then(() => {
      if (disposed) return
      setConnected(true)
      void refresh()
    }).catch(() => {
      if (!disposed) setConnected(false)
    })
    void refresh(controller.signal)
    const refreshWhenActive = () => {
      if (document.visibilityState === "visible") void refresh()
    }
    const timer = window.setInterval(() => {
      if (connection.state !== HubConnectionState.Connected) {
        refreshWhenActive()
        if (connection.state === HubConnectionState.Disconnected) {
          void connection.start().then(() => {
            if (!disposed) {
              setConnected(true)
              void refresh()
            }
          }).catch(() => setConnected(false))
        }
      }
    }, 30_000)
    window.addEventListener("focus", refreshWhenActive)
    document.addEventListener("visibilitychange", refreshWhenActive)
    return () => {
      disposed = true
      controller.abort()
      requestVersion.current++
      window.clearInterval(timer)
      window.removeEventListener("focus", refreshWhenActive)
      document.removeEventListener("visibilitychange", refreshWhenActive)
      connection.off("NotificationCreated")
      connection.off("NotificationReadUpdated")
      void connection.stop()
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
    connected: enabled && ownerToken === accessToken && connected,
    loading: (enabled && ownerToken !== accessToken) || loading,
    error,
    refresh,
    markRead,
    dismissToast,
  }
}
