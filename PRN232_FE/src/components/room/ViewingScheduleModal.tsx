import { useState } from "react"

type ViewingScheduleModalProps = { roomCode?: string; onClose: () => void }

export default function ViewingScheduleModal({ roomCode, onClose }: ViewingScheduleModalProps) {
  const [date, setDate] = useState("")
  const [time, setTime] = useState("")
  const [submitted, setSubmitted] = useState(false)

  function submit(event: React.FormEvent) {
    event.preventDefault()
    setSubmitted(true)
    if (date) window.setTimeout(onClose, 900)
  }

  return <div className="fixed inset-0 z-[70] grid place-items-center bg-slate-950/45 p-6">
    <form onSubmit={submit} className="w-full max-w-[460px] rounded-2xl bg-white p-7 shadow-2xl">
      <div className="flex items-start justify-between">
        <div><p className="text-[11px] font-bold uppercase tracking-[.16em] text-[#087775]">TrọViệt {roomCode ? `· Phòng ${roomCode}` : ""}</p><h2 className="mt-2 font-display text-[24px] font-bold tracking-[-.04em] text-slate-900">Đặt lịch xem phòng</h2></div>
        <button type="button" onClick={onClose} className="grid h-8 w-8 place-items-center rounded-full bg-slate-50 text-lg text-slate-500">×</button>
      </div>
      <p className="mt-3 text-sm leading-6 text-slate-500">Chọn ngày bạn muốn đến xem phòng. Khung giờ là thông tin không bắt buộc.</p>
      <label className="mt-6 block text-sm font-semibold text-slate-700">Ngày xem phòng
        <input type="date" required value={date} onChange={(event) => setDate(event.target.value)} className="mt-2 h-11 w-full rounded-xl border border-slate-200 px-3 text-sm outline-none focus:border-[#087775]" />
      </label>
      <label className="mt-4 block text-sm font-semibold text-slate-700">Giờ xem phòng <span className="font-normal text-slate-400">(không bắt buộc)</span>
        <input type="time" value={time} onChange={(event) => setTime(event.target.value)} className="mt-2 h-11 w-full rounded-xl border border-slate-200 px-3 text-sm outline-none focus:border-[#087775]" />
      </label>
      {submitted && !date && <p className="mt-3 text-sm text-rose-600">Vui lòng chọn ngày xem phòng.</p>}
      {submitted && date && <p className="mt-3 text-sm text-emerald-600">Đã đặt lịch xem phòng thành công.</p>}
      <button type="submit" className="mt-6 w-full rounded-lg bg-[#087775] py-3.5 text-sm font-bold text-white hover:bg-[#066a68]">Đặt lịch</button>
    </form>
  </div>
}
