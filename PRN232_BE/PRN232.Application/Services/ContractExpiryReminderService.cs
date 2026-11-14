using System.Globalization;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.States;
using PRN232.Domain.Entities;

namespace PRN232.Application.Services;

// Rebuild 3.4 (docs/rebuild/PLAN.md): nhắc khách thuê và quản lý khi hợp đồng sắp hết hạn
// (còn dưới 30 ngày, còn dưới 7 ngày) và khi đã quá hạn mà chưa gia hạn hay trả phòng.
// KHÔNG đổi trạng thái hợp đồng: quá hạn thì khách vẫn đang ở, quản lý vẫn cần lập hóa đơn,
// chốt điện nước và làm thủ tục trả phòng (đều yêu cầu hợp đồng Active).
public sealed class ContractExpiryReminderService(
    IContractExpiryReminderRepository repository,
    TimeProvider clock,
    INotificationRealtimePublisher? realtimePublisher = null) : IContractExpiryReminderService
{
    public const int FirstReminderDays = 30;
    public const int UrgentReminderDays = 7;

    private static readonly TimeZoneInfo BusinessTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    public async Task<int> SendDueRemindersAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), BusinessTimeZone).DateTime);
        var contracts = await repository.GetActiveContractsEndingByAsync(today.AddDays(FirstReminderDays), cancellationToken);
        if (contracts.Count == 0)
        {
            return 0;
        }

        // Một thông báo được nhận diện bằng người nhận + tiêu đề. Tiêu đề chứa ngày kết thúc, nên
        // gia hạn hợp đồng (đổi ngày kết thúc) sẽ tự bắt đầu lại chuỗi nhắc cho hạn mới.
        var sent = await repository.GetSentReminderKeysAsync(contracts.Select(c => c.ContractId).ToArray(), cancellationToken);
        var created = new List<Notifications>();
        foreach (var contract in contracts)
        {
            var title = Title(contract, today);
            foreach (var (userId, body) in Recipients(contract))
            {
                if (sent.Add(Key(userId, title)))
                {
                    created.Add(new Notifications
                    {
                        UserId = userId,
                        Title = title,
                        Body = body,
                        Type = NotificationTypes.Contract,
                        RelatedEntityId = contract.ContractId,
                        IsRead = false
                    });
                }
            }
        }

        if (created.Count == 0)
        {
            return 0;
        }
        await repository.AddNotificationsAsync(created, cancellationToken);
        if (realtimePublisher is not null)
        {
            await realtimePublisher.PublishCreatedAsync(created);
        }
        return created.Count;
    }

    public static string Key(int userId, string title) => $"{userId}|{title}";

    private static string Title(ContractExpiryCandidateReadModel contract, DateOnly today)
    {
        var endDate = contract.EndDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var daysLeft = contract.EndDate.DayNumber - today.DayNumber;
        return daysLeft < 0
            ? $"Hợp đồng phòng {contract.RoomCode} đã hết hạn ngày {endDate} nhưng chưa gia hạn hoặc trả phòng"
            : daysLeft <= UrgentReminderDays
                ? $"Hợp đồng phòng {contract.RoomCode} sẽ hết hạn ngày {endDate} (còn dưới {UrgentReminderDays} ngày)"
                : $"Hợp đồng phòng {contract.RoomCode} sẽ hết hạn ngày {endDate} (còn dưới {FirstReminderDays} ngày)";
    }

    private static IEnumerable<(int UserId, string Body)> Recipients(ContractExpiryCandidateReadModel contract)
    {
        yield return (contract.CustomerId, "Liên hệ quản lý để gia hạn hợp đồng hoặc hẹn ngày trả phòng.");
        foreach (var managerId in contract.ManagerIds)
        {
            yield return (managerId, "Liên hệ khách thuê để gia hạn hợp đồng hoặc làm thủ tục trả phòng.");
        }
    }
}
