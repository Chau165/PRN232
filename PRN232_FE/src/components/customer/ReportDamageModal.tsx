import { useState } from "react"
import type { RentalEquipment } from "@/api/rentalsApi"

type ReportDamageModalProps = {
  roomCode: string
  equipment: RentalEquipment[]
  pending: boolean
  error: string | null
  onClose: () => void
  onSubmit: (form: { equipmentId: number | null; description: string }) => Promise<void>
}

export default function ReportDamageModal({
  roomCode,
  equipment,
  pending,
  error,
  onClose,
  onSubmit,
}: ReportDamageModalProps) {
  const [equipmentId, setEquipmentId] = useState("")
  const [description, setDescription] = useState("")

  async function submit(event: React.FormEvent) {
    event.preventDefault()
    await onSubmit({ equipmentId: equipmentId ? Number(equipmentId) : null, description })
  }

  return (
    <div className="fixed inset-0 z-[70] grid place-items-center bg-slate-950/45 p-5">
      <form
        onSubmit={submit}
        className="max-h-[90vh] w-full max-w-[560px] overflow-y-auto rounded-2xl bg-white p-7 shadow-2xl"
      >
        <div className="flex items-start justify-between">
          <div>
            <p className="text-[11px] font-bold uppercase tracking-[.16em] text-[#087775]">
              Phòng {roomCode}
            </p>
            <h2 className="mt-2 font-display text-[24px] font-bold tracking-[-.04em] text-slate-900">
              Báo cáo hư hỏng
            </h2>
          </div>
          <button
            type="button"
            onClick={onClose}
            disabled={pending}
            aria-label="Đóng"
            className="grid h-8 w-8 place-items-center rounded-full bg-slate-50 text-lg text-slate-500 disabled:opacity-50"
          >
            ×
          </button>
        </div>
        <div className="mt-6 space-y-4">
          <label className="block text-[13px] font-semibold text-slate-700">
            Phòng
            <input
              readOnly
              value={roomCode}
              className="mt-2 h-11 w-full rounded-xl border border-slate-200 bg-slate-50 px-3.5 text-[14px] text-slate-500"
            />
          </label>
          <label className="block text-[13px] font-semibold text-slate-700">
            Thiết bị liên quan{" "}
            <span className="font-normal text-slate-400">(không bắt buộc)</span>
            <select
              value={equipmentId}
              onChange={(event) => setEquipmentId(event.target.value)}
              className="mt-2 h-11 w-full rounded-xl border border-slate-200 bg-white px-3.5 text-[14px] outline-none focus:border-[#087775]"
            >
              <option value="">Chọn thiết bị</option>
              {equipment.map((item) => (
                <option key={item.equipmentId} value={item.equipmentId}>{item.name}</option>
              ))}
            </select>
          </label>
          <label className="block text-[13px] font-semibold text-slate-700">
            Mô tả chi tiết
            <textarea
              required
              value={description}
              onChange={(event) => setDescription(event.target.value)}
              rows={4}
              placeholder="Mô tả tình trạng bạn đang gặp phải..."
              className="mt-2 w-full resize-none rounded-xl border border-slate-200 bg-white px-3.5 py-3 text-[14px] outline-none placeholder:text-slate-400 focus:border-[#087775]"
            />
          </label>
          <p className="text-[12px] text-slate-500">API hiện tại chưa hỗ trợ ảnh báo hỏng hoặc mức độ ưu tiên.</p>
          {error && <p role="alert" className="text-[13px] text-rose-600">{error}</p>}
        </div>
        <div className="mt-7 grid grid-cols-2 gap-3">
          <button
            type="button"
            onClick={onClose}
            disabled={pending}
            className="rounded-lg bg-slate-100 py-3 text-[13px] font-bold text-slate-600 hover:bg-slate-200"
          >
            Hủy
          </button>
          <button
            type="submit"
            disabled={pending}
            className="rounded-lg bg-[#087775] py-3 text-[13px] font-bold text-white shadow-sm hover:bg-[#066a68] disabled:cursor-wait disabled:opacity-60"
          >
            {pending ? "Đang gửi..." : "Gửi báo cáo"}
          </button>
        </div>
      </form>
    </div>
  )
}
