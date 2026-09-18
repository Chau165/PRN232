import CustomerPageShell from "@/components/customer/CustomerPageShell"
import { notifications } from "@/data/customer"

export default function NotificationsPage() {
  return (
    <CustomerPageShell
      title="Thông báo"
      description="Cập nhật mới nhất về hóa đơn, hợp đồng và yêu cầu hỗ trợ của bạn."
    >
      <div className="max-w-3xl space-y-3">
        {notifications.map((item) => (
          <a
            key={item.id}
            href={item.href ?? "#"}
            className={`block rounded-2xl border p-5 transition hover:border-teal-200 hover:shadow-sm ${
              item.read
                ? "border-slate-100 bg-white"
                : "border-teal-100 bg-teal-50/50"
            }`}
          >
            <div className="flex items-start gap-3">
              <span
                className={`mt-1 h-2.5 w-2.5 shrink-0 rounded-full ${
                  item.read ? "bg-slate-200" : "bg-[#087775]"
                }`}
              />
              <div className="min-w-0 flex-1">
                <div className="flex flex-wrap justify-between gap-2">
                  <h2 className="text-[14px] font-bold text-slate-800">
                    {item.title}
                  </h2>
                  <span className="text-[11px] text-slate-400">
                    {item.date}
                  </span>
                </div>
                <p className="mt-1 text-[13px] leading-5 text-slate-500">
                  {item.body}
                </p>
              </div>
            </div>
          </a>
        ))}
      </div>
    </CustomerPageShell>
  )
}
