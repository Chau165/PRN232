import { useState } from "react"
import ManagementDataTable from "@/components/management/ManagementDataTable"
import ManagementPage from "@/components/management/ManagementPage"
import StatusBadge from "@/components/customer/StatusBadge"
import Toast from "@/components/customer/Toast"

type Viewing = { id: string; name: string; phone: string; date: string; room: string; ownerDecides: boolean; status: "Chờ xử lý" | "Đã chấp thuận" | "Đã từ chối"; suggestedDate?: string }

const initialViewings: Viewing[] = [
  { id: "VIEW-001", name: "Nguyễn Trung Nghĩa", phone: "0908 123 456", date: "25/09/2026 · 14:00", room: "A203", ownerDecides: false, status: "Chờ xử lý" },
  { id: "VIEW-002", name: "Trần Minh Khang", phone: "0912 456 789", date: "Chủ trọ sắp xếp", room: "B102", ownerDecides: true, status: "Chờ xử lý" },
]

export default function ManagementViewingsPage() {
  const [viewings, setViewings] = useState(initialViewings)
  const [toast, setToast] = useState("")
  const [picker, setPicker] = useState<string | null>(null)
  const [pickerDate, setPickerDate] = useState("")

  function update(id: string, status: Viewing["status"], message: string) {
    setViewings((items) => items.map((item) => item.id === id ? { ...item, status } : item))
    window.localStorage.setItem("troviet-viewing-update", message)
    setToast(message)
  }

  function saveSchedule() {
    const selected = viewings.find((item) => item.id === picker)
    if (!selected || !selected.ownerDecides || selected.status !== "Chờ xử lý" || !pickerDate) return

    setViewings((items) => items.map((item) => item.id === selected.id ? { ...item, suggestedDate: pickerDate } : item))
    const message = `Đã chọn lịch ${pickerDate} cho ${selected.name}`
    window.localStorage.setItem("troviet-viewing-update", message)
    setToast(message)
    setPicker(null)
    setPickerDate("")
  }

  return <ManagementPage title="Lịch xem phòng" description="Xem và xử lý các lịch hẹn xem phòng từ khách thuê.">
    <ManagementDataTable headers={["Người đặt", "Số điện thoại", "Ngày xem", "Phòng muốn xem", "Trạng thái", "Thao tác"]}>
      <div className="divide-y divide-slate-100">
        {viewings.map((viewing) => <div key={viewing.id} className="grid gap-3 px-5 py-5 lg:grid-cols-[1.2fr_1fr_1.3fr_1fr_1fr_2.1fr] lg:items-center">
          <span className="text-[13px] font-semibold text-slate-700">{viewing.name}<small className="block text-slate-400">{viewing.id}</small></span>
          <span className="text-[13px] text-slate-600">{viewing.phone}</span>
          <span className="text-[13px] text-slate-600">{viewing.suggestedDate ?? viewing.date}</span>
          <span className="text-[13px] font-semibold text-slate-700">Phòng {viewing.room}</span>
          <StatusBadge tone={viewing.status === "Đã chấp thuận" ? "green" : viewing.status === "Đã từ chối" ? "rose" : "amber"}>{viewing.status}</StatusBadge>
          <div className="flex flex-wrap gap-2">
            <button disabled={!viewing.ownerDecides || viewing.status !== "Chờ xử lý"} onClick={() => { setPicker(viewing.id); setPickerDate(viewing.suggestedDate ?? "") }} className="rounded-lg bg-slate-100 px-3 py-2 text-[12px] font-bold text-slate-600 disabled:cursor-not-allowed disabled:opacity-40">Chọn lịch</button>
            <button disabled={viewing.status !== "Chờ xử lý"} onClick={() => update(viewing.id, "Đã từ chối", `Đã từ chối lịch xem phòng của ${viewing.name}`)} className="rounded-lg bg-rose-50 px-3 py-2 text-[12px] font-bold text-rose-600 disabled:opacity-40">Từ chối</button>
            <button disabled={viewing.status !== "Chờ xử lý"} onClick={() => update(viewing.id, "Đã chấp thuận", `Đã chấp thuận lịch xem phòng của ${viewing.name}`)} className="rounded-lg bg-teal-50 px-3 py-2 text-[12px] font-bold text-[#087775] disabled:opacity-40">Chấp thuận</button>
          </div>
        </div>)}
      </div>
    </ManagementDataTable>
    {picker && <div className="fixed inset-0 z-50 grid place-items-center bg-slate-950/40 p-6"><div className="w-full max-w-sm rounded-2xl bg-white p-6 shadow-2xl"><h2 className="font-display text-xl font-bold text-slate-900">Chọn lịch xem phòng</h2><p className="mt-2 text-sm text-slate-500">Lưu ngày và giờ xem phòng. Yêu cầu vẫn chờ xử lý cho đến khi bạn bấm Chấp thuận.</p><input type="datetime-local" value={pickerDate} onChange={(event) => setPickerDate(event.target.value)} className="mt-5 h-11 w-full rounded-xl border border-slate-200 px-3 text-sm" /><div className="mt-5 flex gap-2"><button onClick={() => { setPicker(null); setPickerDate("") }} className="flex-1 rounded-lg bg-slate-100 py-3 text-sm font-bold text-slate-600">Huỷ</button><button disabled={!pickerDate} onClick={saveSchedule} className="flex-1 rounded-lg bg-[#087775] py-3 text-sm font-bold text-white disabled:opacity-40">Lưu lịch</button></div></div></div>}
    {toast && <Toast message={toast} onClose={() => setToast("")} />}
  </ManagementPage>
}
