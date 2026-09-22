import Icon, { type IconName } from "@/components/common/Icon"
import { routes } from "@/constants/routes"
import type { CurrentUser } from "@/types/user"

type ManagementSidebarProps = {
  user: CurrentUser
  pathname: string
  open: boolean
  onClose: () => void
}

type SidebarItem = {
  label: string
  href: string
  icon: IconName
  adminOnly?: boolean
}

const items: SidebarItem[] = [
  { label: "Dashboard", href: routes.managementDashboard, icon: "building" },
  {
    label: "Khu vực",
    href: routes.managementAreas,
    icon: "pin",
    adminOnly: true,
  },
  { label: "Khu trọ", href: routes.managementProperties, icon: "building" },
  { label: "Phòng", href: routes.managementRooms, icon: "home" as IconName },
  {
    label: "Khách thuê",
    href: routes.managementTenants,
    icon: "user" as IconName,
  },
  {
    label: "Đặt phòng & Đặt cọc",
    href: routes.managementBookings,
    icon: "calendar" as IconName,
  },
  {
    label: "Lịch xem phòng",
    href: routes.managementViewings,
    icon: "calendar" as IconName,
  },
  {
    label: "Hợp đồng",
    href: routes.managementContracts,
    icon: "file" as IconName,
  },
  {
    label: "Hóa đơn",
    href: routes.managementInvoices,
    icon: "receipt" as IconName,
  },
  {
    label: "Thiết bị",
    href: routes.managementEquipment,
    icon: "tool" as IconName,
  },
  {
    label: "Bảo trì / Sửa chữa",
    href: routes.managementMaintenance,
    icon: "wrench" as IconName,
  },
  {
    label: "Báo cáo",
    href: routes.managementReports,
    icon: "chart" as IconName,
    adminOnly: true,
  },
  {
    label: "Phân công Manager",
    href: routes.managementManagers,
    icon: "users" as IconName,
    adminOnly: true,
  },
  {
    label: "Người dùng",
    href: routes.managementUsers,
    icon: "user" as IconName,
    adminOnly: true,
  },
]

export default function ManagementSidebar({
  user,
  pathname,
  open,
  onClose,
}: ManagementSidebarProps) {
  return (
    <>
      {open && (
        <button
          aria-label="Đóng menu"
          onClick={onClose}
          className="fixed inset-0 z-40 bg-slate-950/35 lg:hidden"
        />
      )}
      <aside
        className={`fixed inset-y-0 left-0 z-50 flex w-[260px] flex-col border-r border-slate-100 bg-white transition-transform lg:static lg:translate-x-0 ${
          open ? "translate-x-0" : "-translate-x-full"
        }`}
      >
        <div className="flex h-[76px] items-center gap-2.5 border-b border-slate-100 px-6">
          <span className="grid h-9 w-9 place-items-center rounded-xl bg-[#087775] text-white">
            <Icon name="building" size={20} />
          </span>
          <span className="font-display text-[20px] font-bold tracking-[-.05em] text-slate-900">
            Trọ<span className="text-[#087775]">Việt</span>
          </span>
          <button
            onClick={onClose}
            className="ml-auto text-xl text-slate-400 lg:hidden"
          >
            ×
          </button>
        </div>
        <div className="border-b border-slate-100 px-5 py-5">
          <p className="text-[10px] font-bold uppercase tracking-[.14em] text-[#087775]">
            Management Portal
          </p>
          <p className="mt-1 text-[13px] font-semibold text-slate-700">
            {user.role === "ADMIN"
              ? "Quản trị toàn hệ thống"
              : "Khu vực vận hành"}
          </p>
        </div>
        <nav className="flex-1 space-y-1 overflow-y-auto p-4">
          {items
            .filter((item) => !item.adminOnly || user.role === "ADMIN")
            .map((item) => {
              const active =
                pathname === item.href ||
                (item.href !== routes.managementDashboard &&
                  pathname.startsWith(item.href))
              return (
                <a
                  key={item.href}
                  href={item.href}
                  onClick={onClose}
                  className={`flex items-center gap-3 rounded-lg px-3 py-2.5 text-[13px] font-semibold transition ${
                    active
                      ? "bg-teal-50 text-[#087775]"
                      : "text-slate-500 hover:bg-slate-50 hover:text-slate-800"
                  }`}
                >
                  <Icon name={item.icon} size={16} />
                  {item.label}
                </a>
              )
            })}
        </nav>
        <div className="border-t border-slate-100 p-4">
          <a
            href={routes.home}
            className="flex items-center gap-2 rounded-lg px-3 py-2.5 text-[13px] font-semibold text-slate-500 hover:bg-slate-50"
          >
            ← Về trang khách thuê
          </a>
        </div>
      </aside>
    </>
  )
}
