import type { ReactNode } from "react"

type PortalDrawerProps = {
  title: string
  eyebrow?: string
  onClose: () => void
  children: ReactNode
}

export default function PortalDrawer({
  title,
  eyebrow,
  onClose,
  children,
}: PortalDrawerProps) {
  return (
    <div className="fixed inset-0 z-[60] flex justify-end bg-slate-950/40">
      <section className="h-full w-full max-w-[620px] overflow-y-auto bg-white shadow-2xl">
        <header className="sticky top-0 z-10 flex items-start justify-between border-b border-slate-100 bg-white px-7 py-5">
          <div>
            {eyebrow && (
              <p className="text-[11px] font-bold uppercase tracking-[.14em] text-[#087775]">
                {eyebrow}
              </p>
            )}
            <h2 className="mt-1 font-display text-[25px] font-bold tracking-[-.04em] text-slate-900">
              {title}
            </h2>
          </div>
          <button
            onClick={onClose}
            aria-label="Đóng"
            className="grid h-9 w-9 place-items-center rounded-full bg-slate-50 text-xl text-slate-500 hover:bg-slate-100"
          >
            ×
          </button>
        </header>
        <div className="p-7">{children}</div>
      </section>
    </div>
  )
}
