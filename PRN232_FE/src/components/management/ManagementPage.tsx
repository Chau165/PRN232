import type { ReactNode } from "react"
import { Link } from "react-router"
import ManagementLayout from "@/components/management/ManagementLayout"
import { getCurrentUser } from "@/utils/managementAuth"

export type ManagementBreadcrumb = {
  label: string
  to?: string
}

type ManagementPageProps = {
  title: string
  description?: string
  eyebrow?: string
  breadcrumbs?: ManagementBreadcrumb[]
  actions?: ReactNode
  children: ReactNode
}

export default function ManagementPage({
  title,
  description,
  eyebrow,
  breadcrumbs,
  actions,
  children,
}: ManagementPageProps) {
  const user = getCurrentUser()
  const resolvedEyebrow =
    eyebrow ??
    (user.role === "ADMIN" ? "Toàn hệ thống" : "Dữ liệu được phân công")

  return (
    <ManagementLayout user={user} title={title}>
      <div className="mb-6 flex flex-wrap items-end justify-between gap-4 border-b border-slate-200/80 pb-5">
        <div className="min-w-0 flex-1">
          {breadcrumbs && breadcrumbs.length > 0 && (
            <nav
              aria-label="Đường dẫn"
              className="mb-2 flex flex-wrap items-center gap-1.5 text-xs text-slate-500"
            >
              {breadcrumbs.map((crumb, index) => {
                const isLast = index === breadcrumbs.length - 1
                return (
                  <span
                    key={`${crumb.label}-${index}`}
                    className="inline-flex items-center gap-1.5"
                  >
                    {index > 0 && <span aria-hidden="true">›</span>}
                    {crumb.to && !isLast ? (
                      <Link
                        to={crumb.to}
                        className="font-medium text-slate-500 hover:text-[#087775] hover:underline"
                      >
                        {crumb.label}
                      </Link>
                    ) : (
                      <span
                        aria-current={isLast ? "page" : undefined}
                        className={isLast ? "font-semibold text-slate-700" : ""}
                      >
                        {crumb.label}
                      </span>
                    )}
                  </span>
                )
              })}
            </nav>
          )}
          <p className="text-[11px] font-bold uppercase tracking-[.16em] text-[#087775]">
            {resolvedEyebrow}
          </p>
          <h1 className="mt-1 font-display text-[26px] font-bold tracking-[-.035em] text-slate-900 sm:text-[28px]">
            {title}
          </h1>
          {description && (
            <p className="mt-1 text-[14px] text-slate-500">{description}</p>
          )}
        </div>
        {actions && (
          <div className="flex shrink-0 flex-wrap items-center gap-2.5">
            {actions}
          </div>
        )}
      </div>
      {children}
    </ManagementLayout>
  )
}
