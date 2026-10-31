using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface INotificationRepository
{
    Task<List<Notifications>> GetByUserAsync(int userId, bool? isRead, string? type);
    Task<List<int>> GetActiveAdminUserIdsAsync();
    Task<Notifications?> GetByIdAsync(int id);
    Task SaveAsync();
}
