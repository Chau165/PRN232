type LoginRequiredModalProps = {
  onClose: () => void
}

export default function LoginRequiredModal({
  onClose,
}: LoginRequiredModalProps) {
  return (
    <div className="fixed inset-0 z-[70] grid place-items-center bg-slate-950/45 p-6">
      <div className="w-full max-w-[420px] rounded-2xl bg-white p-7 shadow-2xl">
        <div className="flex items-start justify-between">
          <div>
            <p className="text-[11px] font-bold uppercase tracking-[.16em] text-[#087775]">
              TrọViệt
            </p>
            <h2 className="mt-2 font-display text-[24px] font-bold tracking-[-.04em] text-slate-900">
              Đăng nhập để tiếp tục
            </h2>
          </div>
          <button
            onClick={onClose}
            className="grid h-8 w-8 place-items-center rounded-full bg-slate-50 text-lg text-slate-500"
          >
            ×
          </button>
        </div>
        <p className="mt-4 text-[14px] leading-6 text-slate-500">
          Bạn cần đăng nhập hoặc đăng ký tài khoản trước khi đặt lịch xem hoặc
          đặt cọc giữ phòng.
        </p>
        <div className="mt-7 grid grid-cols-2 gap-3">
          <button className="rounded-lg bg-[#087775] py-3 text-[13px] font-bold text-white">
            Đăng nhập
          </button>
          <button className="rounded-lg bg-teal-50 py-3 text-[13px] font-bold text-[#087775]">
            Đăng ký
          </button>
        </div>
        <button
          onClick={onClose}
          className="mt-4 w-full text-[12px] font-semibold text-slate-500 hover:text-slate-700"
        >
          Để sau
        </button>
      </div>
    </div>
  )
}
