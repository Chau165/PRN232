import { act, render, renderHook, screen, waitFor } from "@testing-library/react"
import { MemoryRouter } from "react-router"
import { beforeEach, describe, expect, it, vi } from "vitest"
import { useCustomerNotifications } from "@/hooks/useCustomerNotifications"
import { CustomerNotificationsProvider, useCustomerNotificationsState } from "@/auth/CustomerNotificationsContext"

const mocks = vi.hoisted(() => ({
  getNotifications: vi.fn(),
  markNotificationRead: vi.fn(),
  notifications: [] as Array<{ notificationId: number; title: string; type: string; isRead: boolean }>,
}))
vi.mock("@/api/notificationsApi", () => ({
  getNotifications: mocks.getNotifications,
  markNotificationRead: mocks.markNotificationRead,
}))
vi.mock("@/auth/AuthContext", () => ({
  useAuth: () => ({ status: "authenticated", accessToken: "user-token", user: { role: "ADMIN" } }),
}))

function NotificationCount() {
  const { unreadCount } = useCustomerNotificationsState()
  return <span>unread:{unreadCount}</span>
}

beforeEach(() => {
  vi.clearAllMocks()
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

  it("polls every 15 seconds and auto-hides a toast without marking it read", async () => {
    vi.useFakeTimers()
    try {
      mocks.notifications = [{ notificationId: 1, title: "Old", type: "Maintenance", isRead: false }]
      render(<MemoryRouter><CustomerNotificationsProvider><NotificationCount /></CustomerNotificationsProvider></MemoryRouter>)
      await act(async () => { await Promise.resolve(); await Promise.resolve() })
      expect(screen.queryByRole("status")).toBeNull()
      mocks.notifications = [...mocks.notifications, { notificationId: 2, title: "Manager assigned", type: "Maintenance", isRead: false }]
      await act(async () => { await vi.advanceTimersByTimeAsync(15_000) })
      expect(screen.getByText("Manager assigned")).toBeTruthy()
      expect(screen.getByText("unread:2")).toBeTruthy()
      await act(async () => { await vi.advanceTimersByTimeAsync(8_000) })
      expect(screen.queryByText("Manager assigned")).toBeNull()
      expect(screen.getByText("unread:2")).toBeTruthy()
      expect(mocks.markNotificationRead).not.toHaveBeenCalled()
      expect(mocks.getNotifications).toHaveBeenCalledTimes(4)
    } finally {
      vi.useRealTimers()
    }
  })
})
