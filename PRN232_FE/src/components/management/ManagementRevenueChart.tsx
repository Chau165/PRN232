import type { ManagementReportsData } from "@/services/managementReportsService"
import { formatVnd } from "@/utils/publicFormatting"

export default function ManagementRevenueChart({ months }: { months: ManagementReportsData["months"] }) {
  const maximum = Math.max(...months.map((month) => month.revenue), 0)
  return (
    <figure className="min-w-0 rounded-2xl bg-white p-6 shadow-[0_3px_18px_rgba(16,65,67,.06)]">
      <h3 className="font-display text-[21px] font-bold text-slate-900">Doanh thu theo tháng</h3>
      <p className="mt-2 text-xs text-slate-500">7 kỳ hóa đơn gần nhất · Đơn vị: đồng</p>
      {maximum === 0 && <p className="mt-4 text-sm text-slate-500">Chưa ghi nhận tiền đã thu trong 7 kỳ hóa đơn gần nhất.</p>}
      <ul aria-label="Tiền đã thu theo kỳ hóa đơn" className="mt-6 grid grid-cols-7 gap-2">
        {months.map((month) => (
          <li key={month.key} aria-label={`${month.label}: ${formatVnd(month.revenue)}`} title={`${month.label}: ${formatVnd(month.revenue)}`} className="min-w-0">
            <div aria-hidden="true" className="flex h-48 items-end border-b border-slate-200">
              <div className="w-full rounded-t bg-[#087775]" style={{ height: `${maximum > 0 ? month.revenue / maximum * 100 : 0}%` }} />
            </div>
            <p aria-hidden="true" className="mt-2 text-center text-[10px] text-slate-600">{month.label}</p>
            <p aria-hidden="true" className="mt-1 break-words text-center text-[10px] font-semibold text-slate-700">
              {month.revenue.toLocaleString("vi-VN", { notation: "compact", maximumFractionDigits: 1 })}
            </p>
          </li>
        ))}
      </ul>
      <figcaption className="mt-5 text-xs leading-5 text-slate-500">
        Tổng tiền thanh toán thành công, nhóm theo kỳ hóa đơn; không cộng lại tiền cọc hoặc tín dụng trả trước.
      </figcaption>
    </figure>
  )
}
