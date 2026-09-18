import ManagementPage from "@/components/management/ManagementPage"
import ManagementStatCard from "@/components/management/ManagementStatCard"
import { getCurrentUser } from "@/utils/managementAuth"
import { getManagementScope } from "@/utils/managementScope"

export default function ManagementReportsPage() {
  const user = getCurrentUser()
  const scope = getManagementScope(user)
  const totalRooms = user.role === "ADMIN" ? 180 : scope.rooms.length
  const occupied =
    user.role === "ADMIN"
      ? 132
      : scope.rooms.filter((room) => room.status === "Đang thuê").length
  return (
    <ManagementPage
      title="Báo cáo"
      description={
        user.role === "ADMIN"
          ? "Báo cáo tổng hợp toàn hệ thống theo thời gian thực mock."
          : "Báo cáo giới hạn trong các khu trọ được phân công."
      }
    >
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <ManagementStatCard
          label="Doanh thu tháng"
          value={user.role === "ADMIN" ? "624,5 triệu" : "128 triệu"}
          hint={user.role === "ADMIN" ? "Toàn hệ thống" : "Assigned properties"}
        />
        <ManagementStatCard
          label="Tỷ lệ lấp đầy"
          value={`${Math.round((occupied / totalRooms) * 100)}%`}
          hint={`${occupied}/${totalRooms} phòng đang thuê`}
          tone="green"
        />
        <ManagementStatCard
          label="Hóa đơn còn nợ"
          value={String(
            scope.invoices.filter((invoice) => invoice.status !== "PAID")
              .length,
          )}
          hint="Cần theo dõi"
          tone="amber"
        />
        <ManagementStatCard
          label="Bảo trì hoàn thành"
          value={String(
            scope.maintenance.filter(
              (request) => request.status === "HOÀN THÀNH",
            ).length,
          )}
          hint="Trong kỳ này"
          tone="slate"
        />
      </div>
      <div className="mt-6 grid gap-5 lg:grid-cols-2">
        <section className="rounded-2xl bg-white p-6 shadow-[0_3px_18px_rgba(16,65,67,.06)]">
          <h3 className="font-display text-[21px] font-bold text-slate-900">
            Doanh thu theo tháng
          </h3>
          <div className="mt-7 flex h-48 items-end gap-3 border-b border-l border-slate-100 px-4 pb-0">
            {[48, 62, 54, 72, 68, 84, 76].map((height, index) => (
              <div
                key={index}
                className="flex flex-1 flex-col items-center gap-2"
              >
                <div
                  className="w-full rounded-t-lg bg-[#087775]"
                  style={{ height: `${height}%` }}
                />
                <span className="text-[10px] text-slate-400">T{index + 3}</span>
              </div>
            ))}
          </div>
        </section>
        <section className="rounded-2xl bg-white p-6 shadow-[0_3px_18px_rgba(16,65,67,.06)]">
          <h3 className="font-display text-[21px] font-bold text-slate-900">
            Hợp đồng sắp hết hạn
          </h3>
          <div className="mt-5 space-y-3">
            {scope.contracts.slice(0, 3).map((contract) => (
              <div
                key={contract.id}
                className="flex items-center justify-between rounded-xl bg-slate-50 p-4"
              >
                <div>
                  <p className="text-[13px] font-semibold text-slate-700">
                    {contract.propertyName} · {contract.roomCode}
                  </p>
                  <p className="mt-1 text-[11px] text-slate-400">
                    {contract.tenantName} · hết hạn {contract.endDate}
                  </p>
                </div>
                <span className="text-[12px] font-bold text-amber-600">
                  30 ngày
                </span>
              </div>
            ))}
          </div>
        </section>
      </div>
    </ManagementPage>
  )
}
