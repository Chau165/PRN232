type ToastProps = {
  message: string
  onClose: () => void
}

export default function Toast({ message, onClose }: ToastProps) {
  return (
    <div className="fixed bottom-6 right-6 z-[90] flex items-center gap-4 rounded-xl bg-slate-900 px-4 py-3 text-[13px] font-semibold text-white shadow-xl">
      <span className="grid h-5 w-5 place-items-center rounded-full bg-emerald-400 text-slate-900">
        ✓
      </span>
      {message}
      <button
        onClick={onClose}
        aria-label="Đóng thông báo"
        className="text-slate-300 hover:text-white"
      >
        ×
      </button>
    </div>
  )
}
