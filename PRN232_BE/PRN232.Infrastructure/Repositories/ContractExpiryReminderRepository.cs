using Microsoft.EntityFrameworkCore;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.Services;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;

namespace PRN232.Infrastructure.Repositories;

public sealed class ContractExpiryReminderRepository(AppDbContext context) : IContractExpiryReminderRepository
{
    public Task<List<ContractExpiryCandidateReadModel>> GetActiveContractsEndingByAsync(
        DateOnly lastEndDate, CancellationToken cancellationToken) =>
        context.Contracts
            .AsNoTracking()
            .Where(contract => contract.Status == ContractStatuses.Active && contract.EndDate <= lastEndDate)
            .OrderBy(contract => contract.ContractId)
            .Select(contract => new ContractExpiryCandidateReadModel(
                contract.ContractId,
                contract.CustomerId,
                contract.Room!.RoomCode,
                contract.EndDate,
                contract.Room.Property!.ManagerAssignments
                    .Where(assignment => assignment.Manager!.IsActive &&
                                         assignment.Manager.Role!.RoleName == RoleNames.Manager)
                    .Select(assignment => assignment.ManagerId)
                    .ToList()))
            .ToListAsync(cancellationToken);

    public async Task<HashSet<string>> GetSentReminderKeysAsync(
        IReadOnlyCollection<int> contractIds, CancellationToken cancellationToken)
    {
        var sent = await context.Notifications
            .AsNoTracking()
            .Where(notification => notification.Type == NotificationTypes.Contract &&
                                   notification.RelatedEntityId != null &&
                                   contractIds.Contains(notification.RelatedEntityId.Value))
            .Select(notification => new { notification.UserId, notification.Title })
            .ToListAsync(cancellationToken);
        return sent.Select(item => ContractExpiryReminderService.Key(item.UserId, item.Title)).ToHashSet();
    }

    public async Task AddNotificationsAsync(IReadOnlyList<Notifications> notifications, CancellationToken cancellationToken)
    {
        context.Notifications.AddRange(notifications);
        await context.SaveChangesAsync(cancellationToken);
    }
}
