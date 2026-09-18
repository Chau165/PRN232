import type { ReactNode } from "react"
import Footer from "@/components/common/Footer"
import Navbar from "@/components/common/Navbar"

type CustomerPageShellProps = {
  eyebrow?: string
  title: string
  description?: string
  children: ReactNode
}

export default function CustomerPageShell({
  eyebrow = "KHU VỰC CỦA BẠN",
  title,
  description,
  children,
}: CustomerPageShellProps) {
  return (
    <main className="min-h-screen bg-[#fcfdfd] text-slate-800">
      <Navbar />
      <div className="mx-auto max-w-[1180px] px-6 pb-20 pt-12">
        <p className="text-[12px] font-bold uppercase tracking-[.16em] text-[#087775]">
          {eyebrow}
        </p>
        <h1 className="mt-2 font-display text-[34px] font-bold tracking-[-.05em] text-slate-900">
          {title}
        </h1>
        {description && (
          <p className="mt-2 max-w-2xl text-[15px] leading-6 text-slate-500">
            {description}
          </p>
        )}
        <div className="mt-9">{children}</div>
      </div>
      <Footer />
    </main>
  )
}
