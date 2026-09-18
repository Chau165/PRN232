import { useState } from "react"
import Icon from "@/components/common/Icon"
import { notifications } from "@/data/customer"
import { routes } from "@/constants/routes"
import type { CurrentUser } from "@/types/user"
import { endMockSession } from "@/utils/authMock"

type ManagementHeaderProps = {
  user: CurrentUser
  title: string
  onMenu: () => void
}

export default function ManagementHeader({
  user,
  title,
  onMenu,
}: ManagementHeaderProps) {
  const [notificationsOpen, setNotificationsOpen] = useState(false)
  const [profileOpen, setProfileOpen] = useState(false)
  const unread = notifications.filter((item) => !item.read).length
  return (
    <header className="relative flex min-h-[76px] items-center justify-between gap-4 border-b border-slate-100 bg-white px-5 py-3 lg:px-8">
      <div className="flex items-center gap-3">
        <button
          onClick={onMenu}
          className="grid h-9 w-9 place-items-center rounded-lg bg-slate-50 text-slate-600 lg:hidden"
          aria-label="Mở menu"
        >
          ☰
        </button>
        <div>
          <p className="text-[11px] font-bold uppercase tracking-[.14em] text-[#087775]">
            Management Portal
          </p>
          <h1 className="text-[20px] font-bold text-slate-900">{title}</h1>
        </div>
      </div>
      <div className="hidden max-w-[320px] flex-1 items-center rounded-lg bg-slate-50 px-3 text-slate-400 md:flex">
        <Icon name="search" size={16} />
        <input
          placeholder="Tìm kiếm..."
          className="min-w-0 flex-1 bg-transparent px-2 py-2 text-[13px] outline-none"
        />
      </div>
      <div className="flex items-center gap-2">
        <div className="relative">
          <button
            onClick={() => setNotificationsOpen((open) => !open)}
            aria-label="Thông báo"
            className="relative grid h-9 w-9 place-items-center rounded-lg text-slate-500 hover:bg-slate-50"
          >
            ◌
            {unread > 0 && (
              <span className="absolute right-0 top-0 grid h-4 min-w-4 place-items-center rounded-full bg-[#087775] px-1 text-[9px] font-bold text-white">
                {unread}
              </span>
            )}
          </button>
          {notificationsOpen && (
            <div className="absolute right-0 top-12 z-50 w-80 rounded-xl border border-slate-100 bg-white p-3 shadow-xl">
              <div className="flex items-center justify-between px-2 pb-2">
                <strong className="font-display text-[17px] text-slate-900">
                  Thông báo
                </strong>
                <a
                  href={routes.notifications}
                  className="text-[11px] font-semibold text-[#087775]"
                >
                  Customer portal
                </a>
              </div>
              {notifications.slice(0, 3).map((item) => (
                <div
                  key={item.id}
                  className="rounded-lg px-2 py-2 hover:bg-slate-50"
                >
                  <p className="text-[12px] font-semibold text-slate-700">
                    {item.title}
                  </p>
                  <p className="mt-1 text-[11px] text-slate-500">{item.date}</p>
                </div>
              ))}
            </div>
          )}
        </div>
        <div className="relative">
          <button
            onClick={() => {
              setProfileOpen((open) => !open)
              setNotificationsOpen(false)
            }}
            aria-haspopup="menu"
            aria-expanded={profileOpen}
            className="flex items-center gap-2 rounded-xl bg-slate-50 py-1.5 pl-1.5 pr-3 text-left hover:bg-slate-100"
          >
            <span className="grid h-8 w-8 place-items-center rounded-full bg-[#dff0ed] text-[11px] font-bold text-[#087775]">
              {user.role === "ADMIN" ? "CT" : "NA"}
            </span>
            <span className="hidden sm:block">
              <strong className="block text-[12px] text-slate-700">
                {user.name}
              </strong>
              <span className="block text-[10px] font-semibold text-[#087775]">
                {user.role}
              </span>
            </span>
            <Icon name="chevron" size={14} />
          </button>
          {profileOpen && (
            <div
              role="menu"
              className="absolute right-0 top-12 z-50 w-56 rounded-2xl border border-slate-100 bg-white p-2 shadow-[0_12px_32px_rgba(16,65,67,.14)]"
            >
              <a
                href={routes.profile}
                role="menuitem"
                className="block rounded-xl px-4 py-3 text-[14px] text-slate-700 hover:bg-teal-50 hover:text-[#087775]"
              >
                Hồ sơ cá nhân
              </a>
              <div className="my-1 border-t border-slate-100" />
              <button
                role="menuitem"
                onClick={() => {
                  endMockSession()
                  window.location.assign(routes.home)
                }}
                className="block w-full rounded-xl px-4 py-3 text-left text-[14px] font-semibold text-rose-600 hover:bg-rose-50"
              >
                Đăng xuất
              </button>
            </div>
          )}
        </div>
      </div>
    </header>
  )
}
