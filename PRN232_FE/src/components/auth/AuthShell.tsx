import type { ReactNode } from "react"
import Icon from "@/components/common/Icon"

export default function AuthShell({ children }: { children: ReactNode }) {
  return (
    <main className="min-h-screen bg-[#fcfdfd] text-slate-800">
      <header className="mx-auto flex h-[76px] max-w-[1180px] items-center justify-between px-6">
        <a href="/" className="flex items-center gap-2.5">
          <span className="grid h-9 w-9 place-items-center rounded-xl bg-[#087775] text-white">
            <Icon name="building" size={20} />
          </span>
          <span className="font-display text-[20px] font-bold tracking-[-.05em] text-slate-900">
            Trọ<span className="text-[#087775]">Việt</span>
          </span>
        </a>
        <a
          href="/"
          className="text-[13px] font-semibold text-slate-500 hover:text-[#087775]"
        >
          ← Quay lại trang chủ
        </a>
      </header>
      <div className="auth-wash flex min-h-[calc(100vh-76px)] items-center justify-center px-6 py-16">
        {children}
      </div>
    </main>
  )
}
