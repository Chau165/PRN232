export interface AuditLogDto {
  auditLogId: number
  createdAtUtc: string
  userId: number | null
  userName: string | null
  role: string | null
  action: string
  httpMethod: string
  path: string
  entityType: string | null
  entityId: string | null
  statusCode: number
  succeeded: boolean
  ipAddress: string | null
  correlationId: string | null
  oldValues: string | null
  newValues: string | null
}

export interface AuditLogQuery {
  userId?: string
  entityType?: string
  entityId?: string
  succeeded?: "" | "true" | "false"
  from?: string
  to?: string
  page?: number
  pageSize?: number
}
