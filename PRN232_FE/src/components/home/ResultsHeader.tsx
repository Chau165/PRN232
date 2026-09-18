import Icon from "@/components/common/Icon"

export default function ResultsHeader() {
  return (
    <div className="mb-5 flex items-end justify-between">
      <p className="text-[14px] font-semibold text-slate-600">42 kết quả</p>
      <button className="flex items-center gap-3 rounded-lg bg-slate-50 px-3.5 py-2.5 text-[13px] font-medium text-slate-600">
        Sắp xếp:{" "}
        <strong className="font-semibold text-slate-800">Phù hợp nhất</strong>
        <Icon name="chevron" size={14} />
      </button>
    </div>
  )
}
