import { useState } from "react"
import type {
  MaintenancePriority,
  MaintenanceRequestForm,
} from "@/types/customer"

type ReportDamageModalProps = {
  roomCode: string
  equipment: string[]
  onClose: () => void
  onSubmit: (form: MaintenanceRequestForm) => void
}

const categories = [
  "Máy lạnh",
  "Điện",
  "Nước",
  "Cửa",
  "Nhà vệ sinh",
  "Thiết bị",
  "Khác",
]
const priorities: MaintenancePriority[] = [
  "Bình thường",
  "Quan trọng",
  "Khẩn cấp",
]

export default function ReportDamageModal({
  roomCode,
  equipment,
  onClose,
  onSubmit,
}: ReportDamageModalProps) {
  const [category, setCategory] = useState(categories[0])
  const [relatedEquipment, setRelatedEquipment] = useState("")
  const [title, setTitle] = useState("")
  const [description, setDescription] = useState("")
  const [priority, setPriority] = useState<MaintenancePriority>("Bình thường")
  const [imageName, setImageName] = useState("")

  function submit(event: React.FormEvent) {
    event.preventDefault()
    onSubmit({
      category,
      relatedEquipment,
      title,
      description,
      priority,
      imageName,
    })
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
            aria-label="Đóng"
            className="grid h-8 w-8 place-items-center rounded-full bg-slate-50 text-lg text-slate-500"
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
            Loại sự cố
            <select
              value={category}
              onChange={(event) => setCategory(event.target.value)}
              className="mt-2 h-11 w-full rounded-xl border border-slate-200 bg-white px-3.5 text-[14px] outline-none focus:border-[#087775]"
            >
              {categories.map((item) => (
                <option key={item}>{item}</option>
              ))}
            </select>
          </label>
          <label className="block text-[13px] font-semibold text-slate-700">
            Thiết bị liên quan{" "}
            <span className="font-normal text-slate-400">(không bắt buộc)</span>
            <select
              value={relatedEquipment}
              onChange={(event) => setRelatedEquipment(event.target.value)}
              className="mt-2 h-11 w-full rounded-xl border border-slate-200 bg-white px-3.5 text-[14px] outline-none focus:border-[#087775]"
            >
              <option value="">Chọn thiết bị</option>
              {equipment.map((item) => (
                <option key={item}>{item}</option>
              ))}
            </select>
          </label>
          <label className="block text-[13px] font-semibold text-slate-700">
            Tiêu đề sự cố
            <input
              required
              value={title}
              onChange={(event) => setTitle(event.target.value)}
              placeholder="Ví dụ: Máy lạnh không hoạt động"
              className="mt-2 h-11 w-full rounded-xl border border-slate-200 bg-white px-3.5 text-[14px] outline-none placeholder:text-slate-400 focus:border-[#087775]"
            />
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
          <label className="block text-[13px] font-semibold text-slate-700">
            Hình ảnh{" "}
            <span className="font-normal text-slate-400">(không bắt buộc)</span>
            <span className="mt-2 flex h-20 cursor-pointer items-center justify-center rounded-xl border border-dashed border-slate-300 bg-slate-50 text-[13px] text-slate-500 hover:border-[#087775] hover:text-[#087775]">
              <input
                type="file"
                accept="image/*"
                onChange={(event) =>
                  setImageName(event.target.files?.[0]?.name ?? "")
                }
                className="sr-only"
              />
              {imageName || "Tải ảnh lên để mô tả rõ hơn"}
            </span>
          </label>
          <fieldset>
            <legend className="text-[13px] font-semibold text-slate-700">
              Mức độ ưu tiên
            </legend>
            <div className="mt-2 flex flex-wrap gap-2">
              {priorities.map((item) => (
                <label
                  key={item}
                  className={`cursor-pointer rounded-full px-3 py-1.5 text-[12px] font-semibold ${
                    priority === item
                      ? "bg-[#087775] text-white"
                      : "bg-slate-100 text-slate-500"
                  }`}
                >
                  <input
                    type="radio"
                    name="priority"
                    value={item}
                    checked={priority === item}
                    onChange={() => setPriority(item)}
                    className="sr-only"
                  />
                  {item}
                </label>
              ))}
            </div>
          </fieldset>
        </div>
        <div className="mt-7 grid grid-cols-2 gap-3">
          <button
            type="button"
            onClick={onClose}
            className="rounded-lg bg-slate-100 py-3 text-[13px] font-bold text-slate-600 hover:bg-slate-200"
          >
            Hủy
          </button>
          <button
            type="submit"
            className="rounded-lg bg-[#087775] py-3 text-[13px] font-bold text-white shadow-sm hover:bg-[#066a68]"
          >
            Gửi báo cáo
          </button>
        </div>
      </form>
    </div>
  )
}
