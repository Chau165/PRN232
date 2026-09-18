import { useState } from "react"
import Icon from "@/components/common/Icon"

export default function FilterChips() {
  const [radius, setRadius] = useState("3km")

  return (
    <div className="mt-4 flex flex-wrap items-center gap-2">
      <span className="mr-1 text-[13px] text-slate-400">Lọc thêm</span>
      <button className="chip">
        <Icon name="gps" size={15} />
        Vị trí hiện tại
      </button>
      <button className="chip">
        Số người ở <Icon name="chevron" size={14} />
      </button>
      <button className="chip">
        Tiện ích <Icon name="chevron" size={14} />
      </button>
      <span className="ml-1 text-[13px] text-slate-400">Bán kính</span>
      <div className="flex rounded-lg bg-slate-100/80 p-0.5">
        {["1km", "3km", "5km", "10km"].map((item) => (
          <button
            key={item}
            onClick={() => setRadius(item)}
            className={`rounded-md px-2.5 py-1.5 text-[12px] font-semibold ${
              radius === item
                ? "bg-white text-[#087775] shadow-sm"
                : "text-slate-500"
            }`}
          >
            {item}
          </button>
        ))}
      </div>
      <button className="ml-auto flex items-center gap-1.5 px-2 text-[13px] font-medium text-slate-500 hover:text-[#087775]">
        <Icon name="sliders" size={15} />
        Bộ lọc nâng cao
      </button>
    </div>
  )
}
