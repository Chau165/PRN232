import { apiRequest, normalizeApiBaseUrl } from "@/api/apiClient"
import type { NotificationDto } from "@/types/notification"

export function getNotifications(accessToken: string, signal?: AbortSignal, isRead?: boolean) {
  const query = isRead === undefined ? "" : `?isRead=${isRead}`
  return apiRequest<NotificationDto[]>(`/notifications${query}`, { accessToken, signal, cache: "no-store" })
}

export function markNotificationRead(accessToken: string, notificationId: number) {
  return apiRequest<void>(`/notifications/${notificationId}`, {
    accessToken,
    method: "PATCH",
    json: { isRead: true },
  })
}

export function notificationHubUrl(): string {
  const apiBase = new URL(normalizeApiBaseUrl(import.meta.env.VITE_API_BASE_URL), window.location.origin)
  if (apiBase.protocol !== "http:" && apiBase.protocol !== "https:") {
    throw new Error("Cấu hình địa chỉ Hub không hợp lệ.")
  }
  const apiPath = apiBase.pathname.replace(/\/+$/, "")
  return `${apiBase.origin}${apiPath.endsWith("/api") ? apiPath.slice(0, -4) : apiPath}/hubs/notifications`
}
