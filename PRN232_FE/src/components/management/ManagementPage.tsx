import type { ReactNode } from "react"
import ManagementLayout from "@/components/management/ManagementLayout"
import { getCurrentUser } from "@/utils/managementAuth"

type ManagementPageProps = {
  title: string
  description?: string
  children: ReactNode
}

export default function ManagementPage({
  title,
  description,
  children,
}: ManagementPageProps) {
  const user = getCurrentUser()
  return (
    <ManagementLayout user={user} title={title}>
      <div className="mb-7 flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="text-[11px] font-bold uppercase tracking-[.16em] text-[#087775]">
            {user.role === "ADMIN" ? "Toàn hệ thống" : "Dữ liệu được phân công"}
          </p>
          <h2 className="mt-1 font-display text-[28px] font-bold tracking-[-.04em] text-slate-900">
            {title}
          </h2>
          {description && (
            <p className="mt-1 text-[14px] text-slate-500">{description}</p>
          )}
        </div>
      </div>
      {children}
    </ManagementLayout>
  )
}
