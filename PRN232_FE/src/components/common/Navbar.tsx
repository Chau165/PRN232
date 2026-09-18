import { useState } from "react"
import { useLocation, useNavigate } from "react-router"
import Icon from "@/components/common/Icon"
import { notifications } from "@/data/customer"
import { routes } from "@/constants/routes"
import { endMockSession } from "@/utils/authMock"
import { getCurrentUser } from "@/utils/managementAuth"
import { scrollToHash } from "@/utils/scrollToHash"

export default function Navbar() {
  const location = useLocation()
  const navigate = useNavigate()
  const authenticated = window.localStorage.getItem("troviet-auth") === "true"
  const currentUser = getCurrentUser()
  const managementUser =
    currentUser.role === "ADMIN" || currentUser.role === "MANAGER"
  const [profileOpen, setProfileOpen] = useState(false)
  const [notificationsOpen, setNotificationsOpen] = useState(false)
  const unreadCount = notifications.filter((item) => !item.read).length
  const searchActive = location.hash === "#tim-phong"
  const areaActive = location.hash === "#khu-vuc"
  const customerRoomActive =
    location.pathname === routes.myRoom ||
    location.pathname.startsWith(`${routes.myRoom}/`)

  function handleAnchorNavigation(
    hash: "#tim-phong" | "#khu-vuc",
    event: React.MouseEvent<HTMLAnchorElement>,
  ) {
    if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return
    event.preventDefault()
    navigate(`${routes.home}${hash}`)
    window.requestAnimationFrame(() => scrollToHash(hash))
  }

  const searchFlowActive =
    location.pathname === routes.propertyDetail ||
    location.pathname === routes.availableRooms

  return (
    <header className="border-b border-slate-100 bg-white">
      <div className="relative mx-auto flex h-[76px] max-w-[1180px] items-center px-6">
        <a href={routes.home} className="flex items-center gap-2.5">
          <span className="grid h-10 w-10 place-items-center rounded-xl bg-[#087775] text-white">
            <Icon name="building" size={22} />
          </span>
          <span className="font-display text-[22px] font-bold tracking-[-.05em] text-slate-900">
            Trọ<span className="text-[#087775]">Việt</span>
          </span>
        </a>
        <nav className="absolute left-1/2 top-0 flex h-full -translate-x-1/2 gap-8 text-[14px] font-semibold">
          <a
            href={`${routes.home}#tim-phong`}
            onClick={(event) => handleAnchorNavigation("#tim-phong", event)}
            aria-current={searchActive || searchFlowActive ? "page" : undefined}
            className={`flex items-center border-b-2 transition-colors ${
              searchActive || searchFlowActive
                ? "border-[#087775] text-[#087775]"
                : "border-transparent text-slate-500 hover:text-[#087775]"
            }`}
          >
            Tìm phòng
          </a>
          <a
            href={`${routes.home}#khu-vuc`}
            onClick={(event) => handleAnchorNavigation("#khu-vuc", event)}
            aria-current={areaActive ? "page" : undefined}
            className={`flex items-center border-b-2 transition-colors ${
              areaActive
                ? "border-[#087775] text-[#087775]"
                : "border-transparent text-slate-500 hover:text-[#087775]"
            }`}
          >
            Khu vực
          </a>
          {authenticated && !managementUser && (
            <a
              href={routes.myRoom}
              aria-current={customerRoomActive ? "page" : undefined}
              className={`flex items-center border-b-2 transition-colors ${
                customerRoomActive
                  ? "border-[#087775] text-[#087775]"
                  : "border-transparent text-slate-500 hover:text-[#087775]"
              }`}
            >
              Phòng của tôi
            </a>
          )}
        </nav>
        <div className="ml-auto flex items-center gap-2">
          {authenticated ? (
            <>
              {managementUser ? (
                <a
                  href={routes.managementDashboard}
                  className="rounded-lg px-3 py-2.5 text-[14px] font-semibold text-[#087775] hover:bg-teal-50"
                >
                  Quản trị
                </a>
              ) : null}
              <button
                aria-label="Thông báo"
                onClick={() => {
                  setNotificationsOpen((open) => !open)
                  setProfileOpen(false)
                }}
                className="relative grid h-9 w-9 place-items-center rounded-lg text-slate-500 hover:bg-slate-50"
              >
                <span className="text-lg">◌</span>
                {unreadCount > 0 && (
                  <span className="absolute right-1 top-0.5 grid h-4 min-w-4 place-items-center rounded-full bg-[#087775] px-1 text-[9px] font-bold text-white">
                    {unreadCount}
                  </span>
                )}
              </button>
              <button
                onClick={() => {
                  setProfileOpen((open) => !open)
                  setNotificationsOpen(false)
                }}
                className="flex items-center gap-2 rounded-lg py-1.5 pl-1.5 pr-2 text-[13px] font-semibold text-slate-700 hover:bg-slate-50"
              >
                <span className="grid h-7 w-7 place-items-center rounded-full bg-[#dff0ed] text-[11px] text-[#087775]">
                  {managementUser
                    ? currentUser.role === "ADMIN"
                      ? "CT"
                      : "NA"
                    : "NA"}
                </span>
                {managementUser ? currentUser.name : "Ngọc Anh"}{" "}
                <Icon name="chevron" size={13} />
              </button>
              {profileOpen && (
                <div className="absolute right-6 top-[66px] z-50 w-52 rounded-xl border border-slate-100 bg-white p-2 shadow-[0_10px_30px_rgba(16,65,67,.14)]">
                  {managementUser ? (
                    <a
                      href={routes.managementDashboard}
                      className="block rounded-lg px-3 py-2.5 text-[13px] text-slate-600 hover:bg-teal-50 hover:text-[#087775]"
                    >
                      Management Portal
                    </a>
                  ) : (
                    <>
                      <a
                        href={routes.profile}
                        className="block rounded-lg px-3 py-2.5 text-[13px] text-slate-600 hover:bg-teal-50 hover:text-[#087775]"
                      >
                        Hồ sơ cá nhân
                      </a>
                      <a
                        href={routes.myContracts}
                        className="block rounded-lg px-3 py-2.5 text-[13px] text-slate-600 hover:bg-teal-50 hover:text-[#087775]"
                      >
                        Hợp đồng của tôi
                      </a>
                      <a
                        href={routes.myBills}
                        className="block rounded-lg px-3 py-2.5 text-[13px] text-slate-600 hover:bg-teal-50 hover:text-[#087775]"
                      >
                        Hóa đơn
                      </a>
                      <a
                        href={routes.myBookings}
                        className="block rounded-lg px-3 py-2.5 text-[13px] text-slate-600 hover:bg-teal-50 hover:text-[#087775]"
                      >
                        Lịch sử đặt cọc / đặt phòng
                      </a>
                    </>
                  )}
                  <div className="my-1 border-t border-slate-100" />
                  <button
                    onClick={() => {
                      endMockSession()
                      window.location.assign(routes.home)
                    }}
                    className="block w-full rounded-lg px-3 py-2.5 text-left text-[13px] font-semibold text-rose-600 hover:bg-rose-50"
                  >
                    Đăng xuất
                  </button>
                </div>
              )}
              {notificationsOpen && (
                <div className="absolute right-20 top-[66px] z-50 w-80 rounded-xl border border-slate-100 bg-white p-3 shadow-[0_10px_30px_rgba(16,65,67,.14)]">
                  <div className="flex items-center justify-between px-2 pb-2">
                    <p className="font-display text-[17px] font-bold text-slate-900">
                      Thông báo
                    </p>
                    <a
                      href={routes.notifications}
                      className="text-[11px] font-semibold text-[#087775]"
                    >
                      Xem tất cả
                    </a>
                  </div>
                  {notifications.slice(0, 3).map((item) => (
                    <a
                      key={item.id}
                      href={item.href ?? routes.notifications}
                      className={`block rounded-lg px-2 py-2.5 hover:bg-slate-50 ${
                        item.read ? "" : "bg-teal-50/50"
                      }`}
                    >
                      <p className="text-[12px] font-semibold text-slate-700">
                        {item.title}
                      </p>
                      <p className="mt-1 line-clamp-2 text-[11px] leading-4 text-slate-500">
                        {item.body}
                      </p>
                      <p className="mt-1 text-[10px] text-slate-400">
                        {item.date}
                      </p>
                    </a>
                  ))}
                </div>
              )}
            </>
          ) : (
            <a
              href={routes.login}
              className="rounded-lg bg-slate-900 px-5 py-3 text-[14px] font-semibold text-white shadow-sm hover:bg-slate-700"
            >
              Đăng nhập
            </a>
          )}
        </div>
      </div>
    </header>
  )
}
