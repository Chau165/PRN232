import { useEffect, useState } from "react"
import { useAuth } from "@/auth/AuthContext"
import { ApiError } from "@/api/apiClient"
import ManagementPage from "@/components/management/ManagementPage"
import ManagementStatCard from "@/components/management/ManagementStatCard"
import {
  ManagementEmptyState,
  ManagementErrorState,
  ManagementLoadingState,
} from "@/components/management/ManagementState"
import StatusBadge from "@/components/customer/StatusBadge"
import { getManagementDashboard, type ManagementDashboardData } from "@/services/managementDashboardService"

type DashboardState =
  | { status: "loading" }
  | { status: "error"; message: string }
  | { status: "success"; data: ManagementDashboardData }

export default function ManagementDashboardPage() {
  const { accessToken, user } = useAuth()
  const [state, setState] = useState<DashboardState>({ status: "loading" })
  const [reload, setReload] = useState(0)
  const isAdmin = user?.role === "ADMIN"

  useEffect(() => {
    if (!accessToken) return
    const controller = new AbortController()
    setState({ status: "loading" })
    getManagementDashboard(accessToken, controller.signal).then(
      (data) => { if (!controller.signal.aborted) setState({ status: "success", data }) },
      (error: unknown) => {
        if (!controller.signal.aborted) setState({
          status: "error",
          message: error instanceof ApiError && error.status === 403
            ? "Bạn không có quyền xem dữ liệu dashboard."
            : "Không thể tải dashboard. Vui lòng kiểm tra kết nối và thử lại.",
        })
      },
    )
    return () => controller.abort()
  }, [accessToken, reload])

  const data = state.status === "success" ? state.data : null
  const percentage = (count: number) => `${(data && data.totalRooms > 0 ? count / data.totalRooms * 100 : 0)
    .toLocaleString("vi-VN", { maximumFractionDigits: 1 })}% tổng số phòng`
  const stats: Array<{ label: string; value: number; hint: string; tone: "teal" | "slate" | "amber" | "rose" }> = data ? [
    { label: isAdmin ? "Tổng khu trọ" : "Khu trọ phụ trách", value: data.properties.length, hint: isAdmin ? "Toàn hệ thống" : "Trong phạm vi được giao", tone: "teal" },
    { label: "Tổng phòng", value: data.totalRooms, hint: `Từ ${data.properties.length} khu trọ · ${data.reservedRooms} phòng giữ chỗ`, tone: "slate" },
    { label: "Phòng trống", value: data.availableRooms, hint: percentage(data.availableRooms), tone: "amber" },
    { label: "Đang thuê", value: data.occupiedRooms, hint: percentage(data.occupiedRooms), tone: "teal" },
    { label: "Đang sửa chữa", value: data.maintenanceRooms, hint: "Phòng có trạng thái bảo trì", tone: "rose" },
    { label: "Hóa đơn chưa thanh toán", value: data.unpaidInvoices, hint: "Chưa thanh toán hoặc quá hạn", tone: "amber" },
  ] : []

  return (
    <ManagementPage
      title="Dashboard"
      description={
        isAdmin
          ? "Tổng quan hoạt động toàn bộ hệ thống TrọViệt."
          : `Tổng quan các khu trọ được giao cho ${user?.name ?? "bạn"}.`
      }
      actions={
        <button
          type="button"
          onClick={() => setReload((value) => value + 1)}
          disabled={state.status === "loading"}
          className="rounded-lg border border-slate-200 bg-white px-4 py-2 text-sm font-semibold text-slate-600 hover:bg-slate-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-teal-700 disabled:opacity-50"
        >
          Làm mới dữ liệu
        </button>
      }
    >
      {state.status === "loading" && (
        <ManagementLoadingState
          variant="cards"
          rows={6}
          message="Đang tải dashboard..."
        />
      )}
      {state.status === "error" && (
        <ManagementErrorState
          message={state.message}
          retryLabel="Tải lại dashboard"
          onRetry={() => setReload((value) => value + 1)}
        />
      )}
      {data && <>
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
          {stats.map((stat) => <ManagementStatCard key={stat.label} {...stat} value={stat.value.toLocaleString("vi-VN")} />)}
        </div>
        <div className="mt-6 grid gap-5 xl:grid-cols-[1.2fr_.8fr]">
          <section className="min-w-0 rounded-2xl bg-white p-6 shadow-[0_3px_18px_rgba(16,65,67,.06)]">
            <h3 id="dashboard-properties" className="font-display text-[21px] font-bold text-slate-900">Tình trạng khu trọ</h3>
            {data.properties.length === 0 ? (
              <ManagementEmptyState
                compact
                icon="building"
                message="Chưa có khu trọ trong phạm vi quản lý."
              />
            ) : (
              <div role="region" aria-labelledby="dashboard-properties" tabIndex={0} className="mt-5 max-h-96 space-y-4 overflow-y-auto focus-visible:outline-2 focus-visible:outline-teal-700">
                {data.properties.map((property) => (
                  <div key={property.propertyId} className="flex flex-wrap items-center justify-between gap-3 rounded-xl bg-slate-50 p-4">
                    <div className="min-w-0">
                      <p className="break-words font-semibold text-slate-800">{property.propertyName}</p>
                      <p className="mt-1 text-xs text-slate-500">{[property.ward, property.city].filter(Boolean).join(", ")}</p>
                      <p className="mt-1 text-xs text-slate-500">Chủ sở hữu: {property.ownerName}</p>
                    </div>
                    <div className="flex flex-wrap items-center gap-3 text-xs">
                      <span className="text-slate-500">{property.occupiedRooms}/{property.totalRooms} đang thuê</span>
                      <StatusBadge tone={property.availableRooms > 0 ? "green" : "slate"}>{`${property.availableRooms} phòng trống`}</StatusBadge>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </section>
          <section className="min-w-0 rounded-2xl bg-[#f1f8f7] p-6">
            <h3 className="text-xs font-bold uppercase tracking-wider text-[#087775]">Cần chú ý</h3>
            <div className="mt-5 space-y-4 text-sm">
              {data.attention.length === 0 ? (
                <ManagementEmptyState
                  compact
                  icon="wrench"
                  message="Không có yêu cầu bảo trì cần xử lý."
                />
              ) : data.attention.map((request) => (
                <div key={request.requestId} className="border-b border-teal-100 pb-4 last:border-0">
                  <p className="break-words font-semibold text-slate-700">{request.description}</p>
                  <p className="mt-1 text-slate-500">{request.propertyName} · Phòng {request.roomCode ?? "chưa xác định"}</p>
                  <p className="mt-1 text-xs text-slate-600">{request.status === "Pending" ? "Chờ tiếp nhận" : "Đã phân công"}</p>
                </div>
              ))}
            </div>
          </section>
        </div>
      </>}
    </ManagementPage>
  )
}
