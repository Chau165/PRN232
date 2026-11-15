using PRN232.Application.ReadModels;
using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface IContractExpiryReminderRepository
{
    // Hợp đồng đang Active có ngày kết thúc <= lastEndDate (gồm cả hợp đồng đã quá hạn).
    Task<List<ContractExpiryCandidateReadModel>> GetActiveContractsEndingByAsync(
        DateOnly lastEndDate, CancellationToken cancellationToken);

    // Khóa "userId|title" của các thông báo hợp đồng đã gửi cho những hợp đồng này.
    Task<HashSet<string>> GetSentReminderKeysAsync(
        IReadOnlyCollection<int> contractIds, CancellationToken cancellationToken);

    Task AddNotificationsAsync(IReadOnlyList<Notifications> notifications, CancellationToken cancellationToken);
}

public interface IContractExpiryReminderService
{
    // Trả về số thông báo đã tạo.
    Task<int> SendDueRemindersAsync(CancellationToken cancellationToken);
}
