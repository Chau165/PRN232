using Microsoft.EntityFrameworkCore;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;

namespace PRN232.Infrastructure.Repositories;

public class NotificationRepository(AppDbContext context) : INotificationRepository
{
    public Task<List<NotificationDto>> GetByUserAsync(int userId, bool? isRead, string? type) =>
        context.Notifications
            .AsNoTracking()
            .Where(notification => notification.UserId == userId)
            .Where(notification => isRead == null || notification.IsRead == isRead)
            .Where(notification => type == null || notification.Type == type)
            .OrderByDescending(notification => notification.NotificationId)
            .Select(notification => new NotificationDto(
                notification.NotificationId, notification.Title, notification.Type, notification.IsRead))
            .ToListAsync();

    public Task<List<int>> GetActiveAdminUserIdsAsync() => context.Users
        .AsNoTracking()
        .Where(user => user.IsActive && user.Role!.RoleName == RoleNames.Admin)
        .Select(user => user.UserId)
        .ToListAsync();

    public Task<Notifications?> GetByIdAsync(int id) =>
        context.Notifications.SingleOrDefaultAsync(notification => notification.NotificationId == id);

    public async Task SaveAsync() => await context.SaveChangesAsync();
}
