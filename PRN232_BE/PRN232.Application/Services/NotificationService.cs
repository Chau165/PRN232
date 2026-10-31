using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.States;
using PRN232.Domain.Entities;

namespace PRN232.Application.Services;

public class NotificationService : INotificationService
{
    private readonly INotificationRepository _repository;

    public NotificationService(INotificationRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<NotificationDto>> GetAllAsync(int userId, bool? isRead, string? type)
    {
        var notifications = await _repository.GetByUserAsync(userId, isRead, type?.Trim());
        return notifications.Select(ConvertToDto).ToList();
    }

    public async Task UpdateReadStateAsync(int id, int userId, bool? isRead)
    {
        if (!isRead.HasValue)
        {
            throw new NotificationBusinessRuleException("IsRead is required.");
        }

        var notification = await _repository.GetByIdAsync(id);
        if (notification is null)
        {
            throw new NotificationNotFoundException("Notification does not exist.");
        }
        if (notification.UserId != userId)
        {
            throw new NotificationAuthorizationException("You cannot update another user's notification.");
        }

        notification.IsRead = isRead.Value;
        await _repository.SaveAsync();
    }

    public Task<List<int>> GetActiveAdminUserIdsAsync()
    {
        return _repository.GetActiveAdminUserIdsAsync();
    }

    // Build schema-backed rows so the maintenance repository can save them atomically.
    public IReadOnlyList<Notifications> BuildMaintenanceNotifications(IEnumerable<int> userIds, string title)
    {
        return userIds.Distinct()
            .Select(userId => new Notifications
            {
                UserId = userId,
                Title = title,
                Type = NotificationTypes.Maintenance,
                IsRead = false
            })
            .ToList();
    }

    private static NotificationDto ConvertToDto(Notifications entity)
    {
        return new NotificationDto(
            entity.NotificationId,
            entity.Title,
            entity.Type,
            entity.IsRead);
    }
}

public sealed class NotificationBusinessRuleException(string message) : Exception(message);

public sealed class NotificationNotFoundException(string message) : Exception(message);

public sealed class NotificationAuthorizationException(string message) : Exception(message);
