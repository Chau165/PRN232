import type { CSSProperties, ReactNode } from "react"

type ManagementDataTableProps = {
  headers: string[]
  children: ReactNode
}

export default function ManagementDataTable({
  headers,
  children,
}: ManagementDataTableProps) {
  return (
    <div className="overflow-hidden rounded-2xl border border-slate-100 bg-white shadow-[0_3px_18px_rgba(16,65,67,.05)]">
      <div
        className="hidden grid-cols-[repeat(var(--columns),minmax(0,1fr))] gap-4 border-b border-slate-100 bg-slate-50/80 px-5 py-3 text-[11px] font-bold uppercase tracking-[.08em] text-slate-400 lg:grid"
        style={{ "--columns": headers.length } as CSSProperties}
      >
        {headers.map((header) => (
          <span key={header}>{header}</span>
        ))}
      </div>
      {children}
    </div>
  )
}
