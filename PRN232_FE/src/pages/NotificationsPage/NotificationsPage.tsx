import { useState } from "react"
import { useNavigate } from "react-router"
import CustomerPageShell from "@/components/customer/CustomerPageShell"
import { useCustomerNotificationsState } from "@/auth/CustomerNotificationsContext"
import { useAuth } from "@/auth/AuthContext"
import { routes } from "@/constants/routes"

export default function NotificationsPage() {
  const { notifications, loading, error, refresh, markRead } = useCustomerNotificationsState()
  const navigate = useNavigate()
  const { user } = useAuth()
  const [actionError, setActionError] = useState<string | null>(null)

  async function openNotification(notificationId: number, isRead: boolean) {
    setActionError(null)
    try {
      if (!isRead) await markRead(notificationId)
      const notification = notifications.find((item) => item.notificationId === notificationId)
      navigate(notification?.type === "Maintenance" ? (user?.role === "CUSTOMER" ? `${routes.myRoom}?tab=maintenance` : routes.managementMaintenance) : routes.notifications)
    } catch (cause) {
      setActionError(cause instanceof Error ? cause.message : "Không thể cập nhật thông báo.")
    }
  }

  return (
    <CustomerPageShell
      title="Thông báo"
      description="Cập nhật mới nhất về hóa đơn, hợp đồng và yêu cầu hỗ trợ của bạn."
    >
      <div className="max-w-3xl space-y-3">
          {loading && notifications.length === 0 ? <p role="status" className="rounded-xl bg-white p-5 text-[14px] text-slate-500">Đang tải thông báo...</p> : error ? <div role="alert" className="rounded-xl bg-white p-5 text-[14px] text-rose-600"><p>{error}</p><button onClick={() => void refresh()} className="mt-2 font-semibold">Thử lại</button></div> : notifications.length === 0 ? <p role="status" className="rounded-xl border border-dashed border-slate-200 bg-white p-8 text-center text-[14px] text-slate-500">Bạn chưa có thông báo.</p> : notifications.map((item) => (
          <button key={item.notificationId} onClick={() => void openNotification(item.notificationId, item.isRead)} className={`block w-full rounded-2xl border p-5 text-left transition hover:border-teal-200 hover:shadow-sm ${item.isRead ? "border-slate-100 bg-white" : "border-teal-100 bg-teal-50/50"}`}>
            <div className="flex items-start gap-3"><span className={`mt-1 h-2.5 w-2.5 shrink-0 rounded-full ${item.isRead ? "bg-slate-200" : "bg-[#087775]"}`} /><div className="min-w-0 flex-1"><div className="flex flex-wrap justify-between gap-2"><h2 className="text-[14px] font-bold text-slate-800">{item.title}</h2><span className="text-[11px] text-slate-400">{item.isRead ? "Đã đọc" : "Chưa đọc"}</span></div><p className="mt-1 text-xs text-slate-500">{item.type === "Maintenance" ? "Bảo trì / Sửa chữa" : item.type}</p></div></div>
          </button>
        ))}
        {actionError && <p role="alert" className="text-[13px] text-rose-600">{actionError}</p>}
        {notifications.length > 0 && <button onClick={() => void refresh()} className="text-[13px] font-semibold text-[#087775]">Cập nhật thông báo</button>}
      </div>
    </CustomerPageShell>
  )
}
