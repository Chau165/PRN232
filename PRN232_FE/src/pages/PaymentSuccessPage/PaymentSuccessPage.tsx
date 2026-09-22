import { routes } from "@/constants/routes"

export default function PaymentSuccessPage() {
  return <main className="grid min-h-screen place-items-center bg-[#fcfdfd] px-6"><section className="w-full max-w-md rounded-3xl bg-white p-10 text-center shadow-[0_12px_42px_rgba(16,65,67,.10)]"><div className="mx-auto grid h-16 w-16 place-items-center rounded-full bg-emerald-100 text-3xl text-emerald-600">✓</div><h1 className="mt-6 font-display text-3xl font-bold text-slate-900">Thanh toán thành công</h1><p className="mt-3 text-sm leading-6 text-slate-500">Yêu cầu đặt cọc phòng của bạn đã được ghi nhận. Chủ trọ sẽ liên hệ để xác nhận thông tin.</p><a href={routes.home} className="mt-8 inline-block rounded-lg bg-[#087775] px-6 py-3 text-sm font-bold text-white">Về trang chủ</a></section></main>
}
