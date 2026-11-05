export interface NotificationDto {
  notificationId: number
  title: string
  type: string
  isRead: boolean
  relatedEntityId?: number | null
  body?: string | null
}
