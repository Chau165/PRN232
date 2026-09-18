type ManagementStatCardProps = {
  label: string
  value: string
  hint?: string
  tone?: "teal" | "green" | "amber" | "rose" | "slate"
}

const tones = {
  teal: "bg-teal-50 text-[#087775]",
  green: "bg-emerald-50 text-emerald-700",
  amber: "bg-amber-50 text-amber-700",
  rose: "bg-rose-50 text-rose-700",
  slate: "bg-slate-100 text-slate-700",
}

export default function ManagementStatCard({
  label,
  value,
  hint,
  tone = "teal",
}: ManagementStatCardProps) {
  return (
    <div className="rounded-2xl bg-white p-5 shadow-[0_3px_18px_rgba(16,65,67,.06)]">
      <div
        className={`grid h-9 w-9 place-items-center rounded-lg text-[13px] font-bold ${tones[tone]}`}
      >
        ↗
      </div>
      <p className="mt-4 text-[13px] text-slate-500">{label}</p>
      <p className="mt-1 text-[26px] font-bold tracking-[-.04em] text-slate-900">
        {value}
      </p>
      {hint && <p className="mt-1 text-[11px] text-slate-400">{hint}</p>}
    </div>
  )
}
