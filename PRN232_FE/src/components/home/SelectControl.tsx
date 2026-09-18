import Icon from "@/components/common/Icon"

export default function SelectControl({ label }: { label: string }) {
  return (
    <button className="flex h-12 min-w-[166px] items-center justify-between gap-6 border-l border-slate-100 px-5 text-[14px] font-medium text-slate-600 hover:text-slate-900">
      <span>{label}</span>
      <Icon name="chevron" size={16} />
    </button>
  )
}
