import { useEffect, useState, type FormEvent } from "react"
import { useAuth } from "@/auth/AuthContext"
import StatusBadge from "@/components/customer/StatusBadge"
import ManagementDataTable from "@/components/management/ManagementDataTable"
import ManagementDetailDrawer from "@/components/management/ManagementDetailDrawer"
import ManagementPage from "@/components/management/ManagementPage"
import {
  ManagementEmptyState,
  ManagementErrorState,
  ManagementLoadingState,
} from "@/components/management/ManagementState"
import { getAuditLogs } from "@/services/auditLogService"
import type { AuditLogDto, AuditLogQuery } from "@/types/auditLog"
import { totalPages } from "@/types/pagination"
import {
  AUDIT_ENTITY_TYPE_OPTIONS,
  buildAuditDiffRows,
  DEFAULT_AUDIT_LOG_PAGE_SIZE,
  formatAuditActionLabel,
  formatAuditTimeVietnam,
  validateAuditLogFilter,
} from "@/utils/auditLogValidation"

type FilterDraft = {
  userId: string
  entityType: string
  entityId: string
  succeeded: "" | "true" | "false"
  from: string
  to: string
}

const emptyFilters: FilterDraft = {
  userId: "",
  entityType: "",
  entityId: "",
  succeeded: "",
  from: "",
  to: "",
}

export default function ManagementAuditLogsPage() {
  const { accessToken } = useAuth()
  const [draft, setDraft] = useState<FilterDraft>(emptyFilters)
  const [applied, setApplied] = useState<FilterDraft>(emptyFilters)
  const [filterError, setFilterError] = useState<string | null>(null)

  const [items, setItems] = useState<AuditLogDto[]>([])
  const [totalCount, setTotalCount] = useState(0)
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(DEFAULT_AUDIT_LOG_PAGE_SIZE)
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [reload, setReload] = useState(0)

  const [selected, setSelected] = useState<AuditLogDto | null>(null)

  useEffect(() => {
    if (!accessToken) return
    const controller = new AbortController()
    setLoading(true)
    setLoadError(null)

    const query: AuditLogQuery = {
      userId: applied.userId || undefined,
      entityType: applied.entityType || undefined,
      entityId: applied.entityId || undefined,
      succeeded: applied.succeeded || undefined,
      from: applied.from || undefined,
      to: applied.to || undefined,
      page,
      pageSize: DEFAULT_AUDIT_LOG_PAGE_SIZE,
    }

    getAuditLogs(accessToken, query, controller.signal)
      .then((res) => {
        if (controller.signal.aborted) return
        setItems(res.items)
        setTotalCount(res.totalCount)
        setPageSize(res.pageSize || DEFAULT_AUDIT_LOG_PAGE_SIZE)
      })
      .catch((err: unknown) => {
        if (!controller.signal.aborted) {
          setLoadError(
            err instanceof Error
              ? err.message
              : "Không thể tải nhật ký thao tác.",
          )
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) {
          setLoading(false)
        }
      })

    return () => controller.abort()
  }, [accessToken, applied, page, reload])

  function handleApplyFilter(event: FormEvent) {
    event.preventDefault()
    const validationErr = validateAuditLogFilter({
      userId: draft.userId,
      from: draft.from,
      to: draft.to,
      pageSize: DEFAULT_AUDIT_LOG_PAGE_SIZE,
    })
    if (validationErr) {
      setFilterError(validationErr)
      return
    }
    setFilterError(null)
    setPage(1)
    setApplied(draft)
  }

  function handleResetFilter() {
    setDraft(emptyFilters)
    setApplied(emptyFilters)
    setFilterError(null)
    setPage(1)
  }

  const pageCount = totalPages({ pageSize, totalCount })

  const diffData = selected
    ? buildAuditDiffRows(selected.oldValues, selected.newValues)
    : null

  return (
    <ManagementPage
      title="Nhật ký thao tác"
      description="Theo dõi các thao tác ghi dữ liệu trong hệ thống theo giờ Việt Nam (chỉ dành cho Admin)."
    >
      <section
        aria-label="Bộ lọc nhật ký thao tác"
        className="mb-6 rounded-2xl border border-slate-100 bg-white p-5 shadow-[0_3px_18px_rgba(16,65,67,.05)]"
      >
        <form
          noValidate
          onSubmit={handleApplyFilter}
          className="grid gap-3 sm:grid-cols-2 lg:grid-cols-6"
        >
          <label className="text-xs font-semibold text-slate-600">
            Mã người dùng
            <input
              type="text"
              inputMode="numeric"
              value={draft.userId}
              onChange={(e) => {
                setDraft((prev) => ({ ...prev, userId: e.target.value }))
                setFilterError(null)
              }}
              placeholder="VD: 10"
              className="mt-1 w-full rounded-lg border border-slate-200 px-3 py-2 text-sm text-slate-900"
            />
          </label>

          <label className="text-xs font-semibold text-slate-600">
            Loại đối tượng
            <select
              value={draft.entityType}
              onChange={(e) => {
                setDraft((prev) => ({ ...prev, entityType: e.target.value }))
                setFilterError(null)
              }}
              className="mt-1 w-full rounded-lg border border-slate-200 px-3 py-2 text-sm text-slate-900"
            >
              {AUDIT_ENTITY_TYPE_OPTIONS.map((opt) => (
                <option key={opt.value} value={opt.value}>
                  {opt.label}
                </option>
              ))}
            </select>
          </label>

          <label className="text-xs font-semibold text-slate-600">
            Mã đối tượng
            <input
              type="text"
              value={draft.entityId}
              onChange={(e) =>
                setDraft((prev) => ({ ...prev, entityId: e.target.value }))
              }
              placeholder="VD: 12"
              className="mt-1 w-full rounded-lg border border-slate-200 px-3 py-2 text-sm text-slate-900"
            />
          </label>

          <label className="text-xs font-semibold text-slate-600">
            Kết quả
            <select
              value={draft.succeeded}
              onChange={(e) =>
                setDraft((prev) => ({
                  ...prev,
                  succeeded: e.target.value as FilterDraft["succeeded"],
                }))
              }
              className="mt-1 w-full rounded-lg border border-slate-200 px-3 py-2 text-sm text-slate-900"
            >
              <option value="">Tất cả</option>
              <option value="true">Thành công</option>
              <option value="false">Thất bại</option>
            </select>
          </label>

          <label className="text-xs font-semibold text-slate-600">
            Từ ngày (giờ VN)
            <input
              type="date"
              value={draft.from}
              onChange={(e) => {
                setDraft((prev) => ({ ...prev, from: e.target.value }))
                setFilterError(null)
              }}
              className="mt-1 w-full rounded-lg border border-slate-200 px-3 py-2 text-sm text-slate-900"
            />
          </label>

          <label className="text-xs font-semibold text-slate-600">
            Đến ngày (giờ VN)
            <input
              type="date"
              value={draft.to}
              onChange={(e) => {
                setDraft((prev) => ({ ...prev, to: e.target.value }))
                setFilterError(null)
              }}
              className="mt-1 w-full rounded-lg border border-slate-200 px-3 py-2 text-sm text-slate-900"
            />
          </label>

          <div className="flex flex-wrap items-center gap-2 sm:col-span-2 lg:col-span-6">
            <button
              type="submit"
              className="rounded-lg bg-[#087775] px-4 py-2 text-xs font-bold text-white hover:bg-teal-800"
            >
              Lọc nhật ký
            </button>
            <button
              type="button"
              onClick={handleResetFilter}
              className="rounded-lg border border-slate-200 px-4 py-2 text-xs font-semibold text-slate-600 hover:bg-slate-50"
            >
              Xóa bộ lọc
            </button>
            {filterError && (
              <p role="alert" className="text-xs font-semibold text-rose-700">
                {filterError}
              </p>
            )}
          </div>
        </form>
      </section>

      {loadError && (
        <ManagementErrorState
          variant="banner"
          message={loadError}
          onRetry={() => setReload((v) => v + 1)}
        />
      )}

      <ManagementDataTable
        headers={[
          "Thời gian",
          "Người thực hiện",
          "Thao tác",
          "Đối tượng",
          "Kết quả",
          "IP",
        ]}
      >
        {loading ? (
          <ManagementLoadingState message="Đang tải nhật ký thao tác..." />
        ) : items.length === 0 ? (
          <ManagementEmptyState message="Không có bản ghi nhật ký nào phù hợp." />
        ) : (
          <div className="divide-y divide-slate-100">
            {items.map((log) => (
              <button
                key={log.auditLogId}
                type="button"
                onClick={() => setSelected(log)}
                className="grid w-full gap-3 px-5 py-4 text-left transition hover:bg-slate-50 lg:grid-cols-6 lg:items-center"
              >
                <span className="text-xs font-medium text-slate-700">
                  {formatAuditTimeVietnam(log.createdAtUtc)}
                </span>

                <div className="text-xs">
                  <p className="font-semibold text-slate-900">
                    {log.userName ?? "Hệ thống / Ẩn danh"}
                  </p>
                  <p className="text-[11px] text-slate-500">
                    {log.role ?? "—"}
                    {log.userId != null ? ` · #${log.userId}` : ""}
                  </p>
                </div>

                <div className="text-xs">
                  <p className="font-semibold text-slate-900">
                    {formatAuditActionLabel(log.action)}
                  </p>
                  <p className="font-mono text-[11px] text-slate-500">
                    {log.action}
                  </p>
                </div>

                <span className="text-xs font-medium text-slate-700">
                  {log.entityType
                    ? `${log.entityType}${log.entityId ? ` #${log.entityId}` : ""}`
                    : "—"}
                </span>

                <div>
                  <StatusBadge tone={log.succeeded ? "green" : "rose"}>
                    {log.succeeded
                      ? "Thành công"
                      : `Thất bại ${log.statusCode}`}
                  </StatusBadge>
                </div>

                <span className="font-mono text-xs text-slate-600">
                  {log.ipAddress ?? "—"}
                </span>
              </button>
            ))}
          </div>
        )}
      </ManagementDataTable>

      <nav
        aria-label="Phân trang nhật ký thao tác"
        className="mt-4 flex flex-wrap items-center justify-between gap-3 text-sm text-slate-600"
      >
        <span>
          Trang {page}/{pageCount} · {totalCount} bản ghi
        </span>
        <div className="flex gap-2">
          <button
            type="button"
            disabled={loading || page <= 1}
            onClick={() => setPage((value) => Math.max(1, value - 1))}
            className="rounded-lg border border-slate-200 px-3 py-2 text-xs font-bold disabled:opacity-50"
          >
            Trang trước
          </button>
          <button
            type="button"
            disabled={loading || page >= pageCount}
            onClick={() => setPage((value) => value + 1)}
            className="rounded-lg border border-slate-200 px-3 py-2 text-xs font-bold disabled:opacity-50"
          >
            Trang sau
          </button>
        </div>
      </nav>

      {selected && (
        <ManagementDetailDrawer
          title="Chi tiết nhật ký thao tác"
          eyebrow={`Log #${selected.auditLogId}`}
          onClose={() => setSelected(null)}
        >
          <dl className="space-y-3 rounded-xl bg-slate-50 p-5 text-sm">
            <AuditDetailRow
              label="Thời gian (giờ VN)"
              value={formatAuditTimeVietnam(selected.createdAtUtc)}
            />
            <AuditDetailRow
              label="Người thực hiện"
              value={`${selected.userName ?? "Không xác định"} (${selected.role ?? "—"})${selected.userId != null ? ` · #${selected.userId}` : ""}`}
            />
            <AuditDetailRow
              label="Thao tác"
              value={`${formatAuditActionLabel(selected.action)} (${selected.action})`}
            />
            <AuditDetailRow
              label="Đường dẫn"
              value={`${selected.httpMethod} ${selected.path}`}
            />
            <AuditDetailRow
              label="Đối tượng"
              value={
                selected.entityType
                  ? `${selected.entityType}${selected.entityId ? ` #${selected.entityId}` : ""}`
                  : "—"
              }
            />
            <AuditDetailRow
              label="Kết quả HTTP"
              value={`${selected.succeeded ? "Thành công" : "Thất bại"} (${selected.statusCode})`}
            />
            <AuditDetailRow label="IP" value={selected.ipAddress ?? "—"} />
            <AuditDetailRow
              label="Correlation ID"
              value={selected.correlationId ?? "—"}
            />
          </dl>

          {diffData && diffData.rows.length > 0 && (
            <div className="mt-5">
              <h3 className="text-xs font-bold uppercase tracking-wider text-slate-600">
                Thay đổi dữ liệu (Trước / Sau)
              </h3>
              <div className="mt-2 overflow-x-auto rounded-xl border border-slate-200 bg-white">
                <table className="w-full text-left text-xs">
                  <thead className="border-b border-slate-100 bg-slate-50 text-[11px] font-bold uppercase tracking-wider text-slate-500">
                    <tr>
                      <th scope="col" className="px-3 py-2.5">
                        Trường
                      </th>
                      <th scope="col" className="px-3 py-2.5">
                        Trước
                      </th>
                      <th scope="col" className="px-3 py-2.5">
                        Sau
                      </th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {diffData.rows.map((row) => (
                      <tr
                        key={row.key}
                        data-changed={row.changed ? "true" : "false"}
                        className={row.changed ? "bg-amber-50" : "bg-white"}
                      >
                        <td className="px-3 py-2.5 font-semibold text-slate-800">
                          {row.label}
                          {row.label !== row.key && (
                            <span className="mt-0.5 block font-mono text-[10px] font-normal text-slate-400">
                              {row.key}
                            </span>
                          )}
                        </td>
                        <td className="break-words px-3 py-2.5 text-slate-600">
                          {row.before}
                        </td>
                        <td
                          className={`break-words px-3 py-2.5 ${
                            row.changed
                              ? "font-semibold text-amber-950"
                              : "text-slate-700"
                          }`}
                        >
                          {row.after}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {diffData?.fallbackOld && (
            <div className="mt-4">
              <h3 className="text-xs font-bold uppercase tracking-wider text-slate-600">
                Giá trị cũ (oldValues)
              </h3>
              <pre className="mt-1.5 max-h-64 overflow-auto rounded-xl border border-slate-200 bg-slate-900 p-3 font-mono text-xs text-slate-100">
                {diffData.fallbackOld}
              </pre>
            </div>
          )}

          {diffData?.fallbackNew && (
            <div className="mt-4">
              <h3 className="text-xs font-bold uppercase tracking-wider text-slate-600">
                Giá trị mới (newValues)
              </h3>
              <pre className="mt-1.5 max-h-64 overflow-auto rounded-xl border border-slate-200 bg-slate-900 p-3 font-mono text-xs text-slate-100">
                {diffData.fallbackNew}
              </pre>
            </div>
          )}

          {diffData &&
            diffData.rows.length === 0 &&
            !diffData.fallbackOld &&
            !diffData.fallbackNew && (
              <div className="mt-4 rounded-xl border border-slate-100 bg-slate-50/70">
                <ManagementEmptyState
                  compact
                  message="Thao tác này không ghi nhận giá trị trước/sau."
                />
              </div>
            )}
        </ManagementDetailDrawer>
      )}
    </ManagementPage>
  )
}

function AuditDetailRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex flex-wrap justify-between gap-2 border-b border-slate-100 pb-2 last:border-b-0 last:pb-0">
      <dt className="text-xs font-semibold text-slate-500">{label}</dt>
      <dd className="break-all text-right text-xs font-semibold text-slate-900">
        {value}
      </dd>
    </div>
  )
}
