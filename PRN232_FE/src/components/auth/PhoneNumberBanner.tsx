import { Link, useLocation } from "react-router"
import { useOptionalAuth } from "@/auth/AuthContext"
import { routes } from "@/constants/routes"

/**
 * Accounts created with Google start without a phone number. Booking, viewing requests and
 * contracts need one, so remind the user on every page except the Profile page itself.
 */
export default function PhoneNumberBanner() {
  const auth = useOptionalAuth()
  const location = useLocation()
  const user = auth?.user
  if (!user?.phoneMissing || location.pathname === routes.profile) return null

  return (
    <div role="status" className="border-b border-amber-200 bg-amber-50 px-4 py-2.5 text-center text-[13px] text-amber-800">
      Tài khoản của bạn chưa có số điện thoại. Hãy bổ sung để đặt cọc, đặt lịch xem phòng và ký hợp đồng.{" "}
      <Link to={routes.profile} className="font-bold text-amber-900 underline">
        Cập nhật trong trang cá nhân
      </Link>
    </div>
  )
}
