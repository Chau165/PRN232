import { act, render, renderHook, screen, waitFor } from "@testing-library/react"
import { MemoryRouter } from "react-router"
import { beforeEach, describe, expect, it, vi } from "vitest"
import { useCustomerNotifications } from "@/hooks/useCustomerNotifications"
import { CustomerNotificationsProvider, useCustomerNotificationsState } from "@/auth/CustomerNotificationsContext"

const mocks = vi.hoisted(() => ({
  getNotifications: vi.fn(),
  markNotificationRead: vi.fn(),
  notificationHubUrl: vi.fn(() => "/hubs/notifications"),
  connectionStartsFail: false,
  connections: [] as Array<{ handlers: Map<string, (payload: any) => void> }>,
  notifications: [] as Array<{ notificationId: number; title: string; type: string; isRead: boolean }>,
}))
vi.mock("@/services/notificationService", () => ({
  getNotifications: mocks.getNotifications,
  markNotificationRead: mocks.markNotificationRead,
  notificationHubUrl: mocks.notificationHubUrl,
}))
vi.mock("@microsoft/signalr", () => {
  class HubConnectionBuilder {
    withUrl() { return this }
    withAutomaticReconnect() { return this }
    build() {
      const handlers = new Map<string, (payload: any) => void>()
      const connection = {
        state: "Disconnected" as string,
        handlers,
        on(event: string, handler: (payload: any) => void) { handlers.set(event, handler) },
        off(event: string) { handlers.delete(event) },
        onreconnecting() {},
        onreconnected() {},
        onclose() {},
        async start() {
          if (mocks.connectionStartsFail) throw new Error("offline")
          this.state = "Connected"
        },
        async stop() { this.state = "Disconnected" },
      }
      mocks.connections.push(connection)
      return connection
    }
  }
  return { HubConnectionBuilder, HubConnectionState: { Connected: "Connected", Disconnected: "Disconnected" } }
})
vi.mock("@/services/notificationNavigation", () => ({ notificationPath: () => "/notifications" }))
vi.mock("@/auth/AuthContext", () => ({
  useAuth: () => ({ status: "authenticated", accessToken: "user-token", user: { role: "ADMIN" } }),
}))

function NotificationCount() {
  const { unreadCount } = useCustomerNotificationsState()
  return <span>unread:{unreadCount}</span>
}

beforeEach(() => {
  vi.clearAllMocks()
  mocks.connectionStartsFail = false
  mocks.connections = []
  mocks.notifications = []
  mocks.getNotifications.mockImplementation(async (_token: string, _signal?: AbortSignal, isRead?: boolean) =>
    isRead === false ? mocks.notifications.filter((item) => !item.isRead) : [...mocks.notifications],
  )
  mocks.markNotificationRead.mockResolvedValue(undefined)
})

describe("global notification store", () => {
  it("uses first fetch as baseline and emits each new unread notification once", async () => {
    let unread = [{ notificationId: 1, title: "Old", type: "Maintenance", isRead: false }]
    mocks.getNotifications.mockImplementation(async (_token: string, isRead?: boolean) =>
      isRead === false ? unread : [...unread],
    )
    const { result } = renderHook(() => useCustomerNotifications("user-token", true))
    await waitFor(() => expect(result.current.notifications).toHaveLength(1))
    expect(result.current.toasts).toEqual([])

    unread = [...unread, { notificationId: 2, title: "New assignment", type: "Maintenance", isRead: false }]
    await act(async () => { await result.current.refresh() })
    expect(result.current.toasts.map((toast) => toast.notification.notificationId)).toEqual([2])

    await act(async () => { await result.current.refresh() })
    expect(result.current.toasts.map((toast) => toast.notification.notificationId)).toEqual([2])
  })

  it("keeps unread state when a toast is dismissed and clears state on account switch", async () => {
    mocks.getNotifications.mockResolvedValue([{ notificationId: 4, title: "Unread", type: "Maintenance", isRead: false }])
    const { result, rerender } = renderHook(({ token }) => useCustomerNotifications(token, Boolean(token)), {
      initialProps: { token: "one" as string | null },
    })
    await waitFor(() => expect(result.current.unreadCount).toBe(1))
    act(() => result.current.dismissToast(4))
    expect(result.current.unreadCount).toBe(1)
    rerender({ token: "two" })
    expect(result.current.notifications).toEqual([])
    expect(result.current.toasts).toEqual([])
  })

  it("adds one toast and one unread count for a realtime event and suppresses its duplicate", async () => {
    const { result } = renderHook(() => useCustomerNotifications("user-token", true))
    await waitFor(() => expect(result.current.connected).toBe(true))
    const event = mocks.connections[0]?.handlers.get("NotificationCreated")
    const notification = { notificationId: 10, title: "Viewing request", type: "Viewing", isRead: false }
    act(() => event?.(notification))
    act(() => event?.(notification))

    expect(result.current.notifications).toEqual([notification])
    expect(result.current.unreadCount).toBe(1)
    expect(result.current.toasts.map((toast) => toast.notification.notificationId)).toEqual([10])
  })

  it("uses fallback sync only while disconnected, hides the toast without marking read, and suppresses the late duplicate", async () => {
    vi.useFakeTimers()
    try {
      mocks.notifications = [{ notificationId: 1, title: "Old", type: "Maintenance", isRead: false }]
      mocks.connectionStartsFail = true
      render(<MemoryRouter><CustomerNotificationsProvider><NotificationCount /></CustomerNotificationsProvider></MemoryRouter>)
      await act(async () => { await Promise.resolve(); await Promise.resolve() })
      expect(screen.queryByRole("status")).toBeNull()
      mocks.notifications = [...mocks.notifications, { notificationId: 2, title: "Manager assigned", type: "Maintenance", isRead: false }]
      await act(async () => { await vi.advanceTimersByTimeAsync(30_000) })
      expect(screen.getByText("Manager assigned")).toBeTruthy()
      expect(screen.getByText("unread:2")).toBeTruthy()
      act(() => mocks.connections[0]?.handlers.get("NotificationCreated")?.({ notificationId: 2, title: "Manager assigned", type: "Maintenance", isRead: false }))
      expect(screen.getAllByText("Manager assigned")).toHaveLength(1)
      await act(async () => { await vi.advanceTimersByTimeAsync(8_000) })
      expect(screen.queryByText("Manager assigned")).toBeNull()
      expect(screen.getByText("unread:2")).toBeTruthy()
      act(() => mocks.connections[0]?.handlers.get("NotificationCreated")?.({ notificationId: 2, title: "Manager assigned", type: "Maintenance", isRead: false }))
      expect(screen.queryByText("Manager assigned")).toBeNull()
      expect(mocks.markNotificationRead).not.toHaveBeenCalled()
      expect(mocks.getNotifications).toHaveBeenCalledTimes(2)
    } finally {
      vi.useRealTimers()
    }
  })
})
