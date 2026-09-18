type StatusTone = "teal" | "green" | "amber" | "rose" | "slate"

type StatusBadgeProps = {
  children: string
  tone?: StatusTone
}

const toneClasses: Record<StatusTone, string> = {
  teal: "bg-teal-50 text-[#087775]",
  green: "bg-emerald-50 text-emerald-700",
  amber: "bg-amber-50 text-amber-700",
  rose: "bg-rose-50 text-rose-700",
  slate: "bg-slate-100 text-slate-600",
}

export default function StatusBadge({
  children,
  tone = "teal",
}: StatusBadgeProps) {
  return (
    <span
      className={`inline-flex rounded-full px-2.5 py-1 text-[11px] font-bold tracking-[.04em] ${toneClasses[tone]}`}
    >
      {children}
    </span>
  )
}
