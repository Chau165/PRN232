import type { MaintenanceRequest } from "@/types/customer"
import PortalDrawer from "@/components/customer/PortalDrawer"
import StatusBadge from "@/components/customer/StatusBadge"

type MaintenanceDetailDrawerProps = {
  request: MaintenanceRequest
  onClose: () => void
  onConfirm?: (id: string, rating?: number) => void
}

export default function MaintenanceDetailDrawer({
  request,
  onClose,
  onConfirm,
}: MaintenanceDetailDrawerProps) {
  const completed = request.status === "HOÀN THÀNH"

  return (
    <PortalDrawer title={request.title} eyebrow={request.id} onClose={onClose}>
      <div className="flex flex-wrap items-center gap-2">
        <StatusBadge tone={completed ? "green" : "amber"}>
          {request.status}
        </StatusBadge>
        <StatusBadge tone={request.priority === "Khẩn cấp" ? "rose" : "slate"}>
          {request.priority}
        </StatusBadge>
      </div>
      <div className="mt-5 rounded-xl bg-slate-50 p-4 text-[14px]">
        <p>
          <span className="text-slate-400">Phòng:</span>{" "}
          <strong>{request.roomCode}</strong>
        </p>
        <p className="mt-2">
          <span className="text-slate-400">Loại sự cố:</span>{" "}
          <strong>{request.category}</strong>
        </p>
        <p className="mt-2">
          <span className="text-slate-400">Ngày tạo:</span>{" "}
          <strong>{request.createdAt}</strong>
        </p>
      </div>
      <p className="mt-6 text-[14px] leading-7 text-slate-600">
        {request.description}
      </p>
      <div className="mt-8">
        <h3 className="font-display text-[19px] font-bold text-slate-900">
          Tiến độ xử lý
        </h3>
        <div className="mt-5 space-y-4">
          {request.timeline.map((item, index) => (
            <div key={item.label} className="flex gap-3">
              <div className="flex flex-col items-center">
                <span
                  className={`grid h-6 w-6 place-items-center rounded-full text-[11px] font-bold ${
                    item.done
                      ? "bg-[#087775] text-white"
                      : "bg-slate-100 text-slate-400"
                  }`}
                >
                  {item.done ? "✓" : index + 1}
                </span>
                {index < request.timeline.length - 1 && (
                  <span className="mt-1 h-7 w-px bg-slate-200" />
                )}
              </div>
              <div className="pt-0.5">
                <p
                  className={`text-[14px] font-semibold ${
                    item.done ? "text-slate-700" : "text-slate-400"
                  }`}
                >
                  {item.label}
                </p>
                {item.date && (
                  <p className="mt-0.5 text-[12px] text-slate-400">
                    {item.date}
                  </p>
                )}
              </div>
            </div>
          ))}
        </div>
      </div>
      {completed && onConfirm && (
        <div className="mt-8 rounded-2xl bg-[#f1f8f7] p-5">
          <h3 className="font-display text-[19px] font-bold text-slate-900">
            Đã sửa xong?
          </h3>
          <p className="mt-1 text-[13px] text-slate-500">
            Xác nhận để hoàn tất yêu cầu và đánh giá chất lượng xử lý.
          </p>
          <button
            onClick={() => onConfirm(request.id)}
            className="mt-4 w-full rounded-lg bg-[#087775] py-3 text-[13px] font-bold text-white shadow-sm hover:bg-[#066a68]"
          >
            Xác nhận đã sửa xong
          </button>
          <p className="mt-4 text-[12px] font-semibold text-slate-500">
            Đánh giá chất lượng xử lý (không bắt buộc)
          </p>
          <div className="mt-4 flex items-center gap-2">
            {[1, 2, 3, 4, 5].map((rating) => (
              <button
                key={rating}
                onClick={() => onConfirm(request.id, rating)}
                className="text-2xl text-amber-400 hover:scale-110"
                aria-label={`${rating} sao`}
              >
                ★
              </button>
            ))}
          </div>
        </div>
      )}
    </PortalDrawer>
  )
}
