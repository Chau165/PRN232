import type { ReactNode } from "react"

type DetailSectionProps = {
  title: string
  children: ReactNode
}

export default function DetailSection({ title, children }: DetailSectionProps) {
  return (
    <section className="border-t border-slate-100 py-9">
      <h2 className="font-display text-[23px] font-bold tracking-[-.04em] text-slate-900">
        {title}
      </h2>
      {children}
    </section>
  )
}
