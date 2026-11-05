namespace PRN232.Application.DTOs;

public record NotificationDto(
    int NotificationId,
    string Title,
    string Type,
    bool IsRead,
    int? RelatedEntityId = null,
    string? Body = null);

public record UpdateNotificationDto(bool? IsRead);
