namespace PRN232.Application.DTOs;

public record NotificationDto(int NotificationId, string Title, string Type, bool IsRead);

public record UpdateNotificationDto(bool? IsRead);
