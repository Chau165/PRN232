import type { ReactNode } from "react"
import Icon, { type IconName } from "@/components/common/Icon"

type ManagementLoadingStateProps = {
  message?: string
  variant?: "table" | "cards" | "inline"
  rows?: number
  className?: string
}

export function ManagementLoadingState({
  message = "Đang tải dữ liệu...",
  variant = "table",
  rows = 4,
  className,
}: ManagementLoadingStateProps) {
  const spacingClass =
    variant === "inline"
      ? "rounded-2xl border border-slate-100 bg-white px-5 py-6 shadow-[0_3px_18px_rgba(16,65,67,.04)]"
      : variant === "table"
        ? "px-5 py-6"
        : ""
  return (
    <div
      role="status"
      aria-live="polite"
      aria-busy="true"
      className={[spacingClass, className].filter(Boolean).join(" ") || undefined}
    >
      <div className="flex items-center gap-2.5 text-sm font-medium text-slate-600">
        <span
          aria-hidden="true"
          className="h-4 w-4 shrink-0 animate-spin rounded-full border-2 border-[#087775]/25 border-t-[#087775] motion-reduce:animate-none"
        />
        <span>{message}</span>
      </div>

      {variant === "cards" && (
        <div
          aria-hidden="true"
          className="mt-4 grid gap-4 sm:grid-cols-2 xl:grid-cols-3"
        >
          {Array.from({ length: Math.max(3, rows) }, (_, index) => (
            <div
              key={index}
              className="h-36 animate-pulse rounded-2xl border border-slate-100 bg-white p-5 shadow-[0_3px_18px_rgba(16,65,67,.04)] motion-reduce:animate-none"
            >
              <div className="h-3 w-24 rounded bg-slate-100" />
              <div className="mt-4 h-7 w-28 rounded bg-slate-100" />
              <div className="mt-3 h-3 w-40 rounded bg-slate-100" />
            </div>
          ))}
        </div>
      )}

      {variant === "table" && (
        <div aria-hidden="true" className="mt-4 space-y-3">
          {Array.from({ length: rows }, (_, index) => (
            <div
              key={index}
              className="h-11 animate-pulse rounded-xl bg-slate-100/80 motion-reduce:animate-none"
            />
          ))}
        </div>
      )}
    </div>
  )
}

type ManagementEmptyStateProps = {
  title?: string
  message: string
  icon?: IconName
  action?: ReactNode
  compact?: boolean
  role?: "status"
}

export function ManagementEmptyState({
  title,
  message,
  icon = "file",
  action,
  compact = false,
  role = "status",
}: ManagementEmptyStateProps) {
  return (
    <div
      role={role}
      className={
        compact
          ? "flex flex-col items-center justify-center px-4 py-6 text-center"
          : "flex flex-col items-center justify-center px-5 py-10 text-center"
      }
    >
      <span
        aria-hidden="true"
        className="grid h-10 w-10 place-items-center rounded-full bg-slate-100 text-slate-500"
      >
        <Icon name={icon} size={18} />
      </span>
      {title && (
        <p className="mt-3 text-sm font-semibold text-slate-800">{title}</p>
      )}
      <p className="mt-2 max-w-md text-sm text-slate-500">{message}</p>
      {action && <div className="mt-4">{action}</div>}
    </div>
  )
}

type ManagementErrorStateProps = {
  message: string
  onRetry?: () => void
  retryLabel?: string
  retryDisabled?: boolean
  variant?: "card" | "banner"
}

export function ManagementErrorState({
  message,
  onRetry,
  retryLabel = "Thử lại",
  retryDisabled = false,
  variant = "card",
}: ManagementErrorStateProps) {
  if (variant === "banner") {
    return (
      <div className="mb-5 flex flex-wrap items-center justify-between gap-3 rounded-xl border border-rose-200/80 bg-rose-50/90 px-4 py-3 text-sm text-rose-800">
        <div className="flex min-w-0 flex-1 items-center gap-2.5">
          <svg
            aria-hidden="true"
            viewBox="0 0 24 24"
            width="18"
            height="18"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
            className="shrink-0 text-rose-600"
          >
            <circle cx="12" cy="12" r="9" />
            <path d="M12 8v5M12 16h.01" />
          </svg>
          <p role="alert" className="min-w-0 break-words font-medium">
            {message}
          </p>
        </div>
        {onRetry && (
          <button
            type="button"
            disabled={retryDisabled}
            onClick={onRetry}
            className="shrink-0 rounded-lg border border-rose-200 bg-white px-3.5 py-1.5 text-xs font-bold text-rose-700 hover:bg-rose-50 disabled:opacity-50"
          >
            {retryLabel}
          </button>
        )}
      </div>
    )
  }

  return (
    <div className="rounded-2xl border border-rose-200/80 bg-rose-50/70 p-6 text-center shadow-[0_3px_18px_rgba(16,65,67,.04)]">
      <div className="mx-auto grid h-10 w-10 place-items-center rounded-full bg-rose-100 text-rose-600">
        <svg
          aria-hidden="true"
          viewBox="0 0 24 24"
          width="18"
          height="18"
          fill="none"
          stroke="currentColor"
          strokeWidth="2"
          strokeLinecap="round"
          strokeLinejoin="round"
        >
          <circle cx="12" cy="12" r="9" />
          <path d="M12 8v5M12 16h.01" />
        </svg>
      </div>
      <p role="alert" className="mt-3 text-sm font-medium text-rose-800">
        {message}
      </p>
      {onRetry && (
        <button
          type="button"
          disabled={retryDisabled}
          onClick={onRetry}
          className="mt-4 rounded-lg bg-[#087775] px-4 py-2 text-[13px] font-bold text-white hover:bg-[#066a68] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-teal-700 disabled:opacity-50"
        >
          {retryLabel}
        </button>
      )}
    </div>
  )
}

export function ManagementSuccessBanner({ message }: { message: string }) {
  return (
    <div
      role="status"
      className="mb-5 flex items-center gap-2.5 rounded-xl border border-emerald-200/80 bg-emerald-50/90 px-4 py-3 text-sm font-medium text-emerald-800"
    >
      <span aria-hidden="true" className="shrink-0 text-emerald-600">
        <Icon name="check" size={16} />
      </span>
      <span>{message}</span>
    </div>
  )
}
