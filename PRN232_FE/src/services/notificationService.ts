import { apiRequest } from "@/api/apiClient"
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
