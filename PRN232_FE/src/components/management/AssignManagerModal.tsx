import { useState } from "react"
import { managementProperties, managers } from "@/data/management"

type AssignManagerModalProps = {
  managerId: string
  onClose: () => void
  onSave: (propertyIds: string[]) => void
}

export default function AssignManagerModal({
  managerId,
  onClose,
  onSave,
}: AssignManagerModalProps) {
  const manager = managers.find((item) => item.id === managerId) ?? managers[0]
  const [selected, setSelected] = useState(manager.assignedPropertyIds)
  return (
    <div className="fixed inset-0 z-[70] grid place-items-center bg-slate-950/45 p-5">
      <div className="w-full max-w-[500px] rounded-2xl bg-white p-7 shadow-2xl">
        <div className="flex items-start justify-between">
          <div>
            <p className="text-[11px] font-bold uppercase tracking-[.16em] text-[#087775]">
              {manager.email}
            </p>
            <h2 className="mt-2 font-display text-[24px] font-bold text-slate-900">
              Phân công khu trọ
            </h2>
          </div>
          <button
            onClick={onClose}
            aria-label="Đóng"
            className="text-xl text-slate-400"
          >
            ×
          </button>
        </div>
        <p className="mt-4 text-[14px] text-slate-500">
          Chọn các khu trọ {manager.name} được phép quản lý.
        </p>
        <div className="mt-5 space-y-2">
          {managementProperties.map((property) => (
            <label
              key={property.id}
              className="flex items-center gap-3 rounded-xl border border-slate-100 p-3 hover:bg-slate-50"
            >
              <input
                type="checkbox"
                checked={selected.includes(property.id)}
                onChange={(event) =>
                  setSelected((items) =>
                    event.target.checked
                      ? [...items, property.id]
                      : items.filter((id) => id !== property.id),
                  )
                }
                className="h-4 w-4 accent-[#087775]"
              />
              <span>
                <strong className="block text-[13px] text-slate-700">
                  {property.name}
                </strong>
                <small className="text-[11px] text-slate-400">
                  {property.area}
                </small>
              </span>
            </label>
          ))}
        </div>
        <div className="mt-7 grid grid-cols-2 gap-3">
          <button
            onClick={onClose}
            className="rounded-lg bg-slate-100 py-3 text-[13px] font-bold text-slate-600"
          >
            Hủy
          </button>
          <button
            onClick={() => onSave(selected)}
            className="rounded-lg bg-[#087775] py-3 text-[13px] font-bold text-white"
          >
            Lưu phân công
          </button>
        </div>
      </div>
    </div>
  )
}
