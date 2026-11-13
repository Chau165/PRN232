/** Shared paged response returned by list endpoints (A15). */
export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
}

export interface PageQuery {
  page?: number
  pageSize?: number
}

/** Mirrors PageRequest.MaxPageSize on the backend. */
export const MAX_PAGE_SIZE = 100
export const DEFAULT_PAGE_SIZE = 20

export function totalPages(result: Pick<PagedResult<unknown>, "pageSize" | "totalCount">) {
  if (result.pageSize <= 0) return 1
  return Math.max(1, Math.ceil(result.totalCount / result.pageSize))
}
