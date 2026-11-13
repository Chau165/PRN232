using Microsoft.AspNetCore.SignalR;
using PRN232.Api.Hubs;
using PRN232.Application.Interfaces;
using PRN232.Domain.Entities;

namespace PRN232.Api.Services;

public sealed class NotificationRealtimePublisher(
    IHubContext<NotificationHub> hubContext,
    IUserService users,
    ILogger<NotificationRealtimePublisher> logger) : INotificationRealtimePublisher
{
    public async Task PublishCreatedAsync(IEnumerable<Notifications> notifications)
    {
        foreach (var notification in notifications)
        {
            try
            {
                var recipient = await users.GetByIdAsync(notification.UserId);
                if (recipient is null || !recipient.IsActive)
                {
                    continue;
                }

                await hubContext.Clients.Group(NotificationHub.UserGroup(notification.UserId))
                    .SendAsync("NotificationCreated", new
                    {
                        notificationId = notification.NotificationId,
                        title = notification.Title,
                        type = notification.Type,
                        isRead = notification.IsRead,
                        relatedEntityId = notification.RelatedEntityId,
                        body = notification.Body
                    });
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Failed to publish notification {NotificationId} to user {UserId}.",
                    notification.NotificationId, notification.UserId);
            }
        }
    }

    public async Task PublishReadUpdatedAsync(int userId, int notificationId, bool isRead)
    {
        try
        {
            var recipient = await users.GetByIdAsync(userId);
            if (recipient is null || !recipient.IsActive)
            {
                return;
            }

            await hubContext.Clients.Group(NotificationHub.UserGroup(userId))
                .SendAsync("NotificationReadUpdated", new { notificationId, isRead });
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to publish read state for notification {NotificationId} to user {UserId}.",
                notificationId, userId);
        }
    }
}
