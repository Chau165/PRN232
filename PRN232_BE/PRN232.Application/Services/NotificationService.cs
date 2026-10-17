using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Domain.Entities;

namespace PRN232.Application.Services;

public class NotificationService(INotificationRepository repository) : INotificationService
{
    public Task<List<NotificationDto>> GetAllAsync(int userId, bool? isRead, string? type) =>
        repository.GetByUserAsync(userId, isRead, type?.Trim());

    public async Task UpdateReadStateAsync(int id, int userId, bool? isRead)
    {
        if (!isRead.HasValue)
        {
            throw new NotificationBusinessRuleException("IsRead is required.");
        }

        var notification = await repository.GetByIdAsync(id);
        if (notification is null)
        {
            throw new NotificationNotFoundException("Notification does not exist.");
        }
        if (notification.UserId != userId)
        {
            throw new NotificationAuthorizationException("You cannot update another user's notification.");
        }

        notification.IsRead = isRead.Value;
        await repository.SaveAsync();
    }

    public Task<List<int>> GetActiveAdminUserIdsAsync() => repository.GetActiveAdminUserIdsAsync();

    // Build schema-backed rows so the maintenance repository can save them atomically.
    public IReadOnlyList<Notifications> BuildMaintenanceNotifications(IEnumerable<int> userIds, string title) =>
        userIds.Distinct()
            .Select(userId => new Notifications
            {
                UserId = userId,
                Title = title,
                Type = NotificationTypes.Maintenance,
                IsRead = false
            })
            .ToList();
}

public sealed class NotificationBusinessRuleException(string message) : Exception(message);

public sealed class NotificationNotFoundException(string message) : Exception(message);

public sealed class NotificationAuthorizationException(string message) : Exception(message);
