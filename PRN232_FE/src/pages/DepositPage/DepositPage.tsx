import { useState } from "react"
import { useSearchParams } from "react-router"
import CustomerPageShell from "@/components/customer/CustomerPageShell"
import { routes } from "@/constants/routes"
import { rooms } from "@/data/rooms"
import { getCurrentUser } from "@/utils/managementAuth"

export default function DepositPage() {
  const [params] = useSearchParams()
  const room = rooms.find((item) => item.code === params.get("room")) ?? rooms[0]
  const user = getCurrentUser()
  const maxOccupants = Number(room.occupants.match(/\d+/)?.[0] ?? 1)
  const [companionOpen, setCompanionOpen] = useState(false)
  const [companions, setCompanions] = useState<Array<{ name: string; phone: string }>>([])
  const [moveInDate, setMoveInDate] = useState("")
  const [ownerDecides, setOwnerDecides] = useState(false)
  const [payment, setPayment] = useState("")
  const [submitted, setSubmitted] = useState(false)

  const submit = (event: React.FormEvent) => {
    event.preventDefault()
    setSubmitted(true)
    if (payment && (moveInDate || ownerDecides) && (!companionOpen || companions.every((companion) => companion.name.trim()))) {
      window.location.assign(routes.paymentSuccess)
    }
  }

  return (
    <CustomerPageShell title="Đặt cọc giữ phòng" description={`Phòng ${room.code} · Sunrise Residence`}>
      <form onSubmit={submit} className="max-w-2xl space-y-6 rounded-2xl bg-white p-7 shadow-[0_5px_24px_rgba(16,65,67,.08)]">
        <div className="rounded-xl bg-teal-50 p-4 text-sm text-[#087775]">
          Tiền cọc cần thanh toán: <strong>{room.price}</strong> (tương đương 1 tháng tiền thuê)
        </div>
        <div className="grid gap-4 sm:grid-cols-2">
          <ReadOnly label="Họ và tên" value={user.name} />
          <ReadOnly label="Số điện thoại" value={user.phone} />
        </div>
        {maxOccupants > 1 && (
          <section>
            <div className="flex items-center justify-between gap-3">
              <h2 className="font-display text-lg font-bold text-slate-900">Thông tin người ở cùng <span className="text-sm font-normal text-slate-400">(nếu có)</span></h2>
              {companionOpen && (
                <button type="button" onClick={() => { setCompanions([]); setCompanionOpen(false) }} className="text-xs font-semibold text-rose-600 hover:underline">Xoá tất cả</button>
              )}
            </div>
            {!companionOpen ? (
              <button type="button" onClick={() => setCompanionOpen(true)} className="mt-3 rounded-lg border border-dashed border-[#087775] px-4 py-3 text-sm font-semibold text-[#087775] hover:bg-teal-50">+ Thêm người ở cùng</button>
            ) : (
              <>
                <div className="mt-3 space-y-4">
                  {companions.map((companion, index) => (
                    <div key={index} className="rounded-xl border border-slate-100 p-4">
                      <div className="mb-3 flex items-center justify-between">
                        <p className="text-xs font-bold uppercase tracking-[.12em] text-[#087775]">Người ở cùng {index + 1}</p>
                        <button type="button" onClick={() => setCompanions(companions.filter((_, itemIndex) => itemIndex !== index))} className="text-xs font-semibold text-rose-600 hover:underline">Xoá</button>
                      </div>
                      <div className="grid gap-4 sm:grid-cols-2">
                        <Field label="Họ và tên" value={companion.name} onChange={(value) => setCompanions(companions.map((item, itemIndex) => itemIndex === index ? { ...item, name: value } : item))} placeholder="Nhập họ tên người ở cùng" />
                        <Field label="Số điện thoại (không bắt buộc)" value={companion.phone} onChange={(value) => setCompanions(companions.map((item, itemIndex) => itemIndex === index ? { ...item, phone: value } : item))} placeholder="Nhập số điện thoại" />
                      </div>
                    </div>
                  ))}
                </div>
                <p className="mt-2 text-xs text-slate-400">Phòng này cho phép tối đa {maxOccupants} người, bao gồm bạn.</p>
                {companions.length < maxOccupants - 1 && <button type="button" onClick={() => setCompanions([...companions, { name: "", phone: "" }])} className="mt-3 rounded-lg border border-dashed border-[#087775] px-4 py-2.5 text-sm font-semibold text-[#087775] hover:bg-teal-50">+ Thêm người ở cùng</button>}
                {companions.length > 0 && <button type="button" onClick={() => setCompanionOpen(false)} className="ml-3 mt-3 text-xs font-semibold text-[#087775] hover:underline">Lưu thông tin</button>}
              </>
            )}
          </section>
        )}
        <section>
          <h2 className="font-display text-lg font-bold text-slate-900">Ngày nhận phòng mong muốn</h2>
          <div className="mt-3 flex flex-col gap-3">
            <label className="flex items-center gap-2 text-sm text-slate-600"><input type="radio" checked={!ownerDecides} onChange={() => setOwnerDecides(false)} /> Tự chọn ngày</label>
            {!ownerDecides && <input type="date" required={!ownerDecides} value={moveInDate} onChange={(event) => setMoveInDate(event.target.value)} className="h-11 rounded-xl border border-slate-200 px-3 text-sm" />}
            <label className="flex items-center gap-2 text-sm text-slate-600"><input type="radio" checked={ownerDecides} onChange={() => setOwnerDecides(true)} /> Để chủ trọ quyết định ngày nhận phòng</label>
          </div>
        </section>
        <section>
          <h2 className="font-display text-lg font-bold text-slate-900">Phương thức thanh toán</h2>
          <div className="mt-3 grid grid-cols-2 gap-3 sm:grid-cols-3">
            {["MoMo", "Chuyển khoản"].map((method) => <label key={method} className={`cursor-pointer rounded-xl border p-4 text-center text-sm font-semibold ${payment === method ? "border-[#087775] bg-teal-50 text-[#087775]" : "border-slate-200 text-slate-600"}`}><input type="radio" name="payment" value={method} checked={payment === method} onChange={(event) => setPayment(event.target.value)} className="sr-only" />{method}</label>)}
          </div>
        </section>
        {submitted && (!payment || (!moveInDate && !ownerDecides) || (companionOpen && companions.some((companion) => !companion.name.trim()))) && <p className="text-sm text-rose-600">Vui lòng hoàn tất ngày nhận phòng, phương thức thanh toán và họ tên người ở cùng nếu đã thêm.</p>}
        <button type="submit" className="w-full rounded-lg bg-[#087775] py-3.5 text-sm font-bold text-white hover:bg-[#066a68]">Thanh toán {room.price}</button>
      </form>
    </CustomerPageShell>
  )
}

function ReadOnly({ label, value }: { label: string; value: string }) {
  return <label className="block text-sm font-semibold text-slate-700">{label}<input value={value} readOnly className="mt-2 h-11 w-full rounded-xl border border-transparent bg-slate-50 px-3.5 text-sm text-slate-600" /></label>
}

function Field({ label, value, onChange, placeholder }: { label: string; value: string; onChange: (value: string) => void; placeholder: string }) {
  return <label className="block text-sm font-semibold text-slate-700">{label}<input value={value} onChange={(event) => onChange(event.target.value)} placeholder={placeholder} className="mt-2 h-11 w-full rounded-xl border border-slate-200 px-3.5 text-sm outline-none focus:border-[#087775]" /></label>
}
