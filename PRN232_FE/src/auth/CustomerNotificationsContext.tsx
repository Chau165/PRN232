import { createContext, useContext, useEffect, useMemo, useRef, type ReactNode } from "react"
import { useLocation, useNavigate } from "react-router"
import { useAuth } from "@/auth/AuthContext"
import { useCustomerNotifications } from "@/hooks/useCustomerNotifications"
import type { NotificationDto } from "@/types/notification"
import { routes } from "@/constants/routes"

type CustomerNotificationsState = ReturnType<typeof useCustomerNotifications>
const emptyState: CustomerNotificationsState = {
  notifications: [] as NotificationDto[],
  toasts: [],
  unreadCount: 0,
  loading: false,
  error: null as string | null,
  refresh: async () => {},
  markRead: async () => {},
  dismissToast: () => {},
}
const CustomerNotificationsContext = createContext<CustomerNotificationsState | null>(null)

export function CustomerNotificationsProvider({ children }: { children: ReactNode }) {
  const { status, accessToken, user } = useAuth()
  const location = useLocation()
  const navigate = useNavigate()
  const authenticated = status === "authenticated" && user !== null
  const value = useCustomerNotifications(accessToken, authenticated)
  const scopedValue = useMemo<CustomerNotificationsState>(() => authenticated ? value : emptyState, [authenticated, value])
  const previousPath = useRef(location.pathname)
  const previousToken = useRef(accessToken)
  useEffect(() => {
    if (authenticated && previousToken.current === accessToken && previousPath.current !== location.pathname && location.pathname === routes.notifications) {
      void value.refresh()
    }
    if (authenticated && previousToken.current === accessToken && previousPath.current !== location.pathname && location.pathname === routes.managementMaintenance) {
      void value.refresh()
    }
    previousPath.current = location.pathname
    previousToken.current = accessToken
  }, [accessToken, authenticated, location.pathname, value.refresh])
  return <CustomerNotificationsContext.Provider value={scopedValue}>
    {children}
    <div aria-live="polite" aria-label="Thông báo mới" className="pointer-events-none fixed right-4 top-4 z-[120] flex w-[min(24rem,calc(100vw-2rem))] flex-col gap-3">
      {scopedValue.toasts.map(({ notification }) => <NotificationToast key={notification.notificationId} notification={notification} onClose={scopedValue.dismissToast} onOpen={() => {
        scopedValue.dismissToast(notification.notificationId)
        if (notification.type === "Maintenance") {
          navigate(user?.role === "CUSTOMER" ? `${routes.myRoom}?tab=maintenance` : routes.managementMaintenance)
        } else {
          navigate(routes.notifications)
        }
      }} />)}
    </div>
  </CustomerNotificationsContext.Provider>
}

function NotificationToast({ notification, onClose, onOpen }: { notification: NotificationDto; onClose: (notificationId: number) => void; onOpen: () => void }) {
  useEffect(() => {
    const timer = window.setTimeout(() => onClose(notification.notificationId), 8_000)
    return () => window.clearTimeout(timer)
  }, [notification.notificationId, onClose])
  return <section role="status" className="pointer-events-auto relative overflow-hidden rounded-xl border border-teal-200 bg-white shadow-[0_12px_36px_rgba(15,55,60,.22)]">
    <button onClick={onOpen} className="block w-full border-l-4 border-[#087775] px-4 py-3 text-left hover:bg-teal-50">
      <span className="block pr-7 text-sm font-bold text-slate-900">{notification.title}</span>
      <span className="mt-1 block text-xs text-slate-600">{notification.type === "Maintenance" ? "Bảo trì / Sửa chữa" : notification.type}</span>
    </button>
    <button aria-label="Đóng thông báo" onClick={() => onClose(notification.notificationId)} className="absolute right-3 top-3 rounded p-1 text-lg leading-none text-slate-400 hover:bg-slate-100 hover:text-slate-700">×</button>
  </section>
}

export function useCustomerNotificationsState() {
  const state = useContext(CustomerNotificationsContext)
  return state ?? emptyState
}
