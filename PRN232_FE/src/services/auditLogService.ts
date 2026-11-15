import { apiRequest } from "@/api/apiClient"
import type { AuditLogDto, AuditLogQuery } from "@/types/auditLog"
import type { PagedResult } from "@/types/pagination"

export function getAuditLogs(
  accessToken: string,
  query: AuditLogQuery = {},
  signal?: AbortSignal,
) {
  const params = new URLSearchParams()

  if (query.userId?.trim()) params.set("userId", query.userId.trim())
  if (query.entityType?.trim()) params.set("entityType", query.entityType.trim())
  if (query.entityId?.trim()) params.set("entityId", query.entityId.trim())
  if (query.succeeded === "true" || query.succeeded === "false") {
    params.set("succeeded", query.succeeded)
  }
  if (query.from?.trim()) params.set("from", query.from.trim())
  if (query.to?.trim()) params.set("to", query.to.trim())
  if (query.page && query.page >= 1) params.set("page", String(query.page))
  if (query.pageSize && query.pageSize >= 1) {
    params.set("pageSize", String(query.pageSize))
  }

  const queryString = params.toString()
  const path = queryString ? `/audit-logs?${queryString}` : "/audit-logs"

  return apiRequest<PagedResult<AuditLogDto>>(path, {
    accessToken,
    signal,
    cache: "no-store",
  })
}
