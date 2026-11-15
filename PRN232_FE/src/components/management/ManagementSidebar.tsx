import { Link } from "react-router"

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

type SidebarGroup = {
  id: string

  title: string

  items: SidebarItem[]
}

const groups: SidebarGroup[] = [
  {
    id: "overview",
    title: "Tổng quan",
    items: [
      { label: "Dashboard", href: routes.managementDashboard, icon: "building" },
    ],
  },
  {
    id: "properties",
    title: "Khu trọ & Phòng",
    items: [
      { label: "Khu trọ", href: routes.managementProperties, icon: "building" },
    ],
  },
  {
    id: "leasing",
    title: "Cho thuê",
    items: [
      { label: "Lịch xem phòng", href: routes.managementViewings, icon: "calendar" },
      { label: "Đặt phòng & Đặt cọc", href: routes.managementBookings, icon: "calendar" },
      { label: "Hợp đồng", href: routes.managementContracts, icon: "file" },
    ],
  },
  {
    id: "finance",
    title: "Tài chính",
    items: [
      { label: "Hóa đơn", href: routes.managementInvoices, icon: "receipt" },
      {
        label: "Đối soát thanh toán",
        href: routes.managementPaymentReviews,
        icon: "receipt",
        adminOnly: true,
      },
      {
        label: "Xét duyệt hoàn cọc",
        href: routes.managementRefunds,
        icon: "receipt",
        adminOnly: true,
      },
    ],
  },
  {
    id: "operations",
    title: "Vận hành",
    items: [
      { label: "Bảo trì / Sửa chữa", href: routes.managementMaintenance, icon: "wrench" },
    ],
  },
  {
    id: "admin",
    title: "Quản trị",
    items: [
      {
        label: "Người dùng",
        href: routes.managementUsers,
        icon: "user",
        adminOnly: true,
      },
      {
        label: "Phân công Manager",
        href: routes.managementManagers,
        icon: "users",
        adminOnly: true,
      },
      {
        label: "Báo cáo",
        href: routes.managementReports,
        icon: "chart",
        adminOnly: true,
      },
      {
        label: "Nhật ký thao tác",
        href: routes.managementAuditLogs,
        icon: "file",
        adminOnly: true,
      },
    ],
  },
]

export default function ManagementSidebar({
  user,

  pathname,

  open,

  onClose,
}: ManagementSidebarProps) {
  const visibleGroups = groups
    .map((group) => ({
      ...group,
      items: group.items.filter((item) => !item.adminOnly || user.role === "ADMIN"),
    }))
    .filter((group) => group.items.length > 0)

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
        className={`fixed inset-y-0 left-0 z-50 flex w-[260px] flex-col border-r border-slate-100 bg-white transition-transform lg:translate-x-0 ${
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
        <nav aria-label="Điều hướng quản lý" className="flex-1 space-y-5 overflow-y-auto p-4">
          {visibleGroups.map((group) => (
            <div key={group.id} role="group" aria-labelledby={`sidebar-group-${group.id}`}>
              <p
                id={`sidebar-group-${group.id}`}
                className="mb-1.5 px-3 text-[11px] font-bold uppercase tracking-[.12em] text-slate-400"
              >
                {group.title}
              </p>
              <div className="space-y-1">
                {group.items.map((item) => {
                  const active =
                    pathname === item.href ||
                    (item.href !== routes.managementDashboard &&
                      pathname.startsWith(item.href))

                  return (
                    <Link
                      key={item.href}
                      to={item.href}
                      onClick={onClose}
                      aria-current={active ? "page" : undefined}
                      className={`flex items-center gap-3 rounded-lg px-3 py-2.5 text-[13px] font-semibold transition ${
                        active
                          ? "bg-teal-50 text-[#087775]"
                          : "text-slate-500 hover:bg-slate-50 hover:text-slate-800"
                      }`}
                    >
                      <Icon name={item.icon} size={16} />
                      {item.label}
                    </Link>
                  )
                })}
              </div>
            </div>
          ))}
        </nav>
        <div className="border-t border-slate-100 p-4">
          <Link
            to={routes.home}
            className="flex items-center gap-2 rounded-lg px-3 py-2.5 text-[13px] font-semibold text-slate-500 hover:bg-slate-50"
          >
            ← Về trang khách thuê
          </Link>
        </div>
      </aside>
    </>
  )
}
