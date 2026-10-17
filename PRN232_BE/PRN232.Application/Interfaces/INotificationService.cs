using PRN232.Application.DTOs;
using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface INotificationService
{
    Task<List<NotificationDto>> GetAllAsync(int userId, bool? isRead, string? type);
    Task UpdateReadStateAsync(int id, int userId, bool? isRead);
    Task<List<int>> GetActiveAdminUserIdsAsync();
    IReadOnlyList<Notifications> BuildMaintenanceNotifications(IEnumerable<int> userIds, string title);
}
