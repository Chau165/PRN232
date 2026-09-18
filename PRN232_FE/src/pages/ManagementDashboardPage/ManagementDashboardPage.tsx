import ManagementPage from "@/components/management/ManagementPage"
import ManagementStatCard from "@/components/management/ManagementStatCard"
import StatusBadge from "@/components/customer/StatusBadge"
import { getCurrentUser } from "@/utils/managementAuth"
import { getManagementScope } from "@/utils/managementScope"

export default function ManagementDashboardPage() {
  const user = getCurrentUser()
  const scope = getManagementScope(user)
  const occupied = scope.rooms.filter(
    (room) => room.status === "Đang thuê",
  ).length
  const available = scope.rooms.filter((room) => room.status === "Trống").length
  const maintenance = scope.rooms.filter(
    (room) => room.status === "Đang sửa chữa",
  ).length
  const unpaid = scope.invoices.filter(
    (invoice) => invoice.status !== "PAID",
  ).length
  const stats =
    user.role === "ADMIN"
      ? [
          [
            "Tổng khu trọ",
            String(scope.properties.length),
            "Toàn hệ thống",
            "teal",
          ],
          ["Tổng phòng", "180", "Từ 10 khu trọ", "slate"],
          ["Phòng trống", "30", "16,7% tổng số phòng", "amber"],
          ["Đang thuê", "132", "73,3% tổng số phòng", "teal"],
          ["Đang sửa chữa", "10", "Cần xử lý", "rose"],
          ["Hóa đơn chưa thanh toán", "18", "Cần theo dõi", "amber"],
        ]
      : [
          [
            "Khu trọ phụ trách",
            String(scope.properties.length),
            scope.properties.map((property) => property.name).join(" · "),
            "teal",
          ],
          [
            "Tổng phòng",
            String(scope.rooms.length),
            "Trong phạm vi được giao",
            "slate",
          ],
          ["Đang thuê", String(occupied), "Phòng đang có khách", "teal"],
          ["Phòng trống", String(available), "Có thể cho thuê", "amber"],
          ["Đang sửa chữa", String(maintenance), "Cần xử lý", "rose"],
          [
            "Hóa đơn cần theo dõi",
            String(unpaid),
            "Trong phạm vi phụ trách",
            "amber",
          ],
        ]
  return (
    <ManagementPage
      title="Dashboard"
      description={
        user.role === "ADMIN"
          ? "Tổng quan hoạt động toàn bộ hệ thống TrọViệt."
          : `Tổng quan các khu trọ được giao cho ${user.name}.`
      }
    >
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        {stats.map(([label, value, hint, tone]) => (
          <ManagementStatCard
            key={label}
            label={label}
            value={value}
            hint={hint}
            tone={tone as "teal" | "amber" | "rose" | "slate"}
          />
        ))}
      </div>
      <div className="mt-6 grid gap-5 xl:grid-cols-[1.2fr_.8fr]">
        <section className="rounded-2xl bg-white p-6 shadow-[0_3px_18px_rgba(16,65,67,.06)]">
          <div className="flex items-center justify-between">
            <h3 className="font-display text-[21px] font-bold text-slate-900">
              Tình trạng khu trọ
            </h3>
            <span className="text-[12px] text-slate-400">Cập nhật hôm nay</span>
          </div>
          <div className="mt-5 space-y-4">
            {scope.properties.map((property) => (
              <div
                key={property.id}
                className="flex flex-wrap items-center justify-between gap-3 rounded-xl bg-slate-50 p-4"
              >
                <div>
                  <p className="font-semibold text-slate-800">
                    {property.name}
                  </p>
                  <p className="mt-1 text-[12px] text-slate-500">
                    {property.area} · {property.managerName ?? "Chưa phân công"}
                  </p>
                </div>
                <div className="flex items-center gap-4 text-[12px]">
                  <span className="text-slate-500">
                    {property.occupiedRooms}/{property.totalRooms} đang thuê
                  </span>
                  <StatusBadge tone="green">
                    {property.status.toUpperCase()}
                  </StatusBadge>
                </div>
              </div>
            ))}
          </div>
        </section>
        <section className="rounded-2xl bg-[#f1f8f7] p-6">
          <p className="text-[11px] font-bold uppercase tracking-[.14em] text-[#087775]">
            Cần chú ý
          </p>
          <div className="mt-5 space-y-4 text-[13px]">
            {scope.maintenance.slice(0, 3).map((request) => (
              <div
                key={request.id}
                className="border-b border-teal-100 pb-4 last:border-0"
              >
                <p className="font-semibold text-slate-700">
                  {request.problem}
                </p>
                <p className="mt-1 text-slate-500">
                  {request.propertyName} · Phòng {request.roomCode}
                </p>
                <p className="mt-1 text-[11px] text-slate-400">
                  {request.createdDate}
                </p>
              </div>
            ))}
          </div>
        </section>
      </div>
    </ManagementPage>
  )
}
