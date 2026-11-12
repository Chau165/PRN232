using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface INotificationRealtimePublisher
{
    Task PublishCreatedAsync(IEnumerable<Notifications> notifications);
    Task PublishReadUpdatedAsync(int userId, int notificationId, bool isRead);
}
