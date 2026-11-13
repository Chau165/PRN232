using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;

namespace PRN232.Infrastructure.Repositories;

public sealed class PropertyManagerAssignmentRepository : IPropertyManagerAssignmentRepository
{
    private const string AssignmentPrimaryKeyName = "PK_PropertyManagerAssignments";
    private readonly AppDbContext _context;

    public PropertyManagerAssignmentRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<bool> PropertyExistsAsync(int propertyId, CancellationToken cancellationToken)
    {
        return _context.Properties.AnyAsync(
            property => property.PropertyId == propertyId,
            cancellationToken);
    }

    public Task<bool> IsOwnedByAdminAsync(
        int propertyId,
        int adminId,
        CancellationToken cancellationToken)
    {
        return _context.Properties.AnyAsync(
            property => property.PropertyId == propertyId && property.OwnerId == adminId,
            cancellationToken);
    }

    public Task<PropertyOwnerReadModel?> GetPropertyOwnerAsync(
        int propertyId,
        CancellationToken cancellationToken)
    {
        return _context.Properties
            .AsNoTracking()
            .Where(property => property.PropertyId == propertyId)
            .Select(property => new PropertyOwnerReadModel(
                property.PropertyId,
                property.OwnerId,
                property.Owner!.FullName,
                property.Owner.Role!.RoleName))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<bool> IsActiveManagerAsync(int managerId, CancellationToken cancellationToken)
    {
        return _context.Users.AnyAsync(
            user => user.UserId == managerId &&
                user.IsActive &&
                user.Role!.RoleName == RoleNames.Manager,
            cancellationToken);
    }

    public async Task<PropertyManagerAssignmentReadModel?> AddIfActiveAssignmentAbsentAsync(
        PropertyManagerAssignment assignment,
        int adminId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginTransactionIfRelationalAsync(cancellationToken);
        await LockPropertyAsync(assignment.PropertyId, cancellationToken);

        var adminOwnsProperty = await IsOwnedByAdminAsync(
            assignment.PropertyId,
            adminId,
            cancellationToken);
        var managerIsActive = await IsActiveManagerAsync(assignment.ManagerId, cancellationToken);
        var assignmentAlreadyExists = await _context.PropertyManagerAssignments.AnyAsync(
            existing => existing.PropertyId == assignment.PropertyId &&
                existing.ManagerId == assignment.ManagerId,
            cancellationToken);

        if (!adminOwnsProperty || !managerIsActive || assignmentAlreadyExists)
        {
            await RollbackIfNeededAsync(transaction, cancellationToken);
            return null;
        }

        await _context.PropertyManagerAssignments.AddAsync(assignment, cancellationToken);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsAssignmentPrimaryKeyViolation(exception))
        {
            _context.Entry(assignment).State = EntityState.Detached;
            await RollbackIfNeededAsync(transaction, cancellationToken);
            return null;
        }

        var created = await ProjectAssignments(_context.PropertyManagerAssignments
                .AsNoTracking()
                .Where(existing => existing.PropertyId == assignment.PropertyId &&
                    existing.ManagerId == assignment.ManagerId))
            .FirstOrDefaultAsync(cancellationToken);
        if (created is null)
        {
            throw new InvalidOperationException("Created Manager assignment could not be read.");
        }

        await CommitIfNeededAsync(transaction, cancellationToken);
        return created;
    }

    public Task<List<PropertyManagerAssignmentReadModel>> GetByPropertyIdAsync(
        int propertyId,
        CancellationToken cancellationToken)
    {
        var assignments = _context.PropertyManagerAssignments
            .AsNoTracking()
            .Where(assignment => assignment.PropertyId == propertyId)
            .OrderBy(assignment => assignment.Manager!.FullName)
            .ThenBy(assignment => assignment.ManagerId);

        return ProjectAssignments(assignments)
            .ToListAsync(cancellationToken);
    }

    public Task<List<PropertyManagerAssignmentReadModel>> GetByPropertyIdsAsync(
        IReadOnlyCollection<int>? propertyIds,
        CancellationToken cancellationToken)
    {
        IQueryable<PropertyManagerAssignment> assignments = _context.PropertyManagerAssignments
            .AsNoTracking();
        if (propertyIds is not null)
        {
            var ids = propertyIds.Distinct().ToArray();
            assignments = assignments.Where(assignment => ids.Contains(assignment.PropertyId));
        }

        var orderedAssignments = assignments
            .OrderBy(assignment => assignment.PropertyId)
            .ThenBy(assignment => assignment.Manager!.FullName)
            .ThenBy(assignment => assignment.ManagerId);

        return ProjectAssignments(orderedAssignments)
            .ToListAsync(cancellationToken);
    }

    public Task<List<PropertyManagerAssignmentReadModel>> GetByManagerIdAsync(
        int managerId,
        bool requireActiveManager,
        CancellationToken cancellationToken)
    {
        var assignments = _context.PropertyManagerAssignments
            .AsNoTracking()
            .Where(assignment => assignment.ManagerId == managerId)
            .Where(assignment => assignment.Manager!.Role!.RoleName == RoleNames.Manager);

        if (requireActiveManager)
        {
            assignments = assignments.Where(assignment => assignment.Manager!.IsActive);
        }

        var orderedAssignments = assignments
            .OrderBy(assignment => assignment.Property!.PropertyName)
            .ThenBy(assignment => assignment.PropertyId);

        return ProjectAssignments(orderedAssignments)
            .ToListAsync(cancellationToken);
    }

    public Task<List<PropertySummaryReadModel>> GetManagedPropertiesAsync(
        int managerId,
        bool requireActiveManager,
        CancellationToken cancellationToken)
    {
        var properties = _context.Properties
            .AsNoTracking()
            .Where(property => property.ManagerAssignments.Any(assignment =>
                assignment.ManagerId == managerId &&
                assignment.Manager!.Role!.RoleName == RoleNames.Manager &&
                (!requireActiveManager || assignment.Manager.IsActive)));

        return properties
            .OrderBy(property => property.PropertyId)
            .Select(property => new PropertySummaryReadModel(
                property.PropertyId,
                property.OwnerId,
                property.Owner!.FullName,
                property.PropertyName,
                property.AddressText,
                property.Ward,
                property.District,
                property.City,
                property.Latitude,
                property.Longitude,
                property.Rooms.Count,
                property.PrimaryImageUrl,
                property.Description))
            .ToListAsync(cancellationToken);
    }

    public Task<List<ManagedPropertyReadModel>> GetManagedPropertiesForManagersAsync(
        IReadOnlyCollection<int>? managerIds,
        CancellationToken cancellationToken)
    {
        var assignments = _context.PropertyManagerAssignments
            .AsNoTracking()
            .Where(assignment => assignment.Manager!.Role!.RoleName == RoleNames.Manager);
        if (managerIds is not null)
        {
            var ids = managerIds.Distinct().ToArray();
            assignments = assignments.Where(assignment => ids.Contains(assignment.ManagerId));
        }

        return assignments
            .OrderBy(assignment => assignment.ManagerId)
            .ThenBy(assignment => assignment.PropertyId)
            .Select(assignment => new ManagedPropertyReadModel(
                assignment.ManagerId,
                new PropertySummaryReadModel(
                    assignment.Property!.PropertyId,
                    assignment.Property.OwnerId,
                    assignment.Property.Owner!.FullName,
                    assignment.Property.PropertyName,
                    assignment.Property.AddressText,
                    assignment.Property.Ward,
                    assignment.Property.District,
                    assignment.Property.City,
                    assignment.Property.Latitude,
                    assignment.Property.Longitude,
                    assignment.Property.Rooms.Count,
                    assignment.Property.PrimaryImageUrl,
                    assignment.Property.Description)))
            .ToListAsync(cancellationToken);
    }

    public async Task<RevokePropertyManagerAssignmentResult> RevokeActiveAsync(
        int propertyId,
        int managerId,
        int adminId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginTransactionIfRelationalAsync(cancellationToken);
        await LockPropertyAsync(propertyId, cancellationToken);

        if (!await IsOwnedByAdminAsync(propertyId, adminId, cancellationToken))
        {
            await RollbackIfNeededAsync(transaction, cancellationToken);
            return RevokePropertyManagerAssignmentResult.NotOwned;
        }

        await LockAssignmentAsync(propertyId, managerId, cancellationToken);
        var assignment = await _context.PropertyManagerAssignments.FirstOrDefaultAsync(
            existing => existing.PropertyId == propertyId && existing.ManagerId == managerId,
            cancellationToken);
        if (assignment is null)
        {
            await RollbackIfNeededAsync(transaction, cancellationToken);
            return RevokePropertyManagerAssignmentResult.NotFound;
        }

        if (await HasOpenWorkAsync(propertyId, managerId, cancellationToken))
        {
            await RollbackIfNeededAsync(transaction, cancellationToken);
            return RevokePropertyManagerAssignmentResult.HasOpenWork;
        }

        _context.PropertyManagerAssignments.Remove(assignment);
        await _context.SaveChangesAsync(cancellationToken);
        await CommitIfNeededAsync(transaction, cancellationToken);
        return RevokePropertyManagerAssignmentResult.Revoked;
    }

    private static IQueryable<PropertyManagerAssignmentReadModel> ProjectAssignments(
        IQueryable<PropertyManagerAssignment> assignments)
    {
        return assignments.Select(assignment => new PropertyManagerAssignmentReadModel(
            assignment.PropertyId,
            assignment.Property!.PropertyName,
            assignment.ManagerId,
            assignment.Manager!.FullName,
            assignment.Manager.Email,
            assignment.Manager.IsActive));
    }

    private async Task<bool> HasOpenWorkAsync(
        int propertyId,
        int managerId,
        CancellationToken cancellationToken)
    {
        var hasPendingViewing = await _context.ViewingSchedules.AnyAsync(viewing =>
            viewing.AssignedTo == managerId &&
            viewing.Room!.PropertyId == propertyId &&
            (viewing.Status == ViewingStatuses.Pending ||
                (viewing.Status == ViewingStatuses.Approved &&
                    (viewing.ScheduledAt == null || viewing.ScheduledAt > DateTime.UtcNow))),
            cancellationToken);
        if (hasPendingViewing)
        {
            return true;
        }

        var hasActiveContract = await _context.Contracts.AnyAsync(contract =>
            contract.ProcessedByStaffId == managerId &&
            contract.Room!.PropertyId == propertyId &&
            contract.Status == ContractStatuses.Active,
            cancellationToken);
        if (hasActiveContract)
        {
            return true;
        }

        return await _context.MaintenanceRequests.AnyAsync(request =>
            request.AssignedTo == managerId &&
            request.Contract!.Room!.PropertyId == propertyId &&
            request.Status != MaintenanceRequestStatuses.Completed,
            cancellationToken);
    }

    private async Task<IDbContextTransaction?> BeginTransactionIfRelationalAsync(
        CancellationToken cancellationToken)
    {
        return _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;
    }

    private async Task LockPropertyAsync(int propertyId, CancellationToken cancellationToken)
    {
        if (_context.Database.IsNpgsql())
        {
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT \"PropertyId\" FROM \"Properties\" WHERE \"PropertyId\" = {propertyId} FOR UPDATE",
                cancellationToken);
        }
    }

    private async Task LockAssignmentAsync(
        int propertyId,
        int managerId,
        CancellationToken cancellationToken)
    {
        if (_context.Database.IsNpgsql())
        {
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT \"PropertyId\" FROM \"PropertyManagerAssignments\" WHERE \"PropertyId\" = {propertyId} AND \"ManagerId\" = {managerId} FOR UPDATE",
                cancellationToken);
        }
    }

    private static async Task RollbackIfNeededAsync(
        IDbContextTransaction? transaction,
        CancellationToken cancellationToken)
    {
        if (transaction is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
        }
    }

    private static async Task CommitIfNeededAsync(
        IDbContextTransaction? transaction,
        CancellationToken cancellationToken)
    {
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
    }

    private static bool IsAssignmentPrimaryKeyViolation(DbUpdateException exception)
    {
        return exception.GetBaseException() is PostgresException postgresException &&
            postgresException.SqlState == PostgresErrorCodes.UniqueViolation &&
            postgresException.ConstraintName == AssignmentPrimaryKeyName;
    }
}
