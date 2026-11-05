using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.Services;
using PRN232.Domain.Entities;
using Xunit;

namespace PRN232.Application.Tests;

public sealed class PropertyManagerAssignmentServiceTests
{
    [Fact]
    public async Task Assign_rejects_manager_even_when_property_exists()
    {
        var repository = new FakePropertyManagerAssignmentRepository
        {
            PropertyExists = true,
            IsOwnedByAdmin = true,
            IsActiveManager = true
        };
        var service = new PropertyManagerAssignmentService(repository);

        await Assert.ThrowsAsync<PropertyManagerAssignmentAuthorizationException>(() =>
            service.AssignManagerAsync(1, 5, 7, "Manager", CancellationToken.None));

        Assert.Null(repository.AddedAssignment);
    }

    [Fact]
    public async Task Assign_rejects_admin_who_does_not_own_the_property()
    {
        var repository = new FakePropertyManagerAssignmentRepository
        {
            PropertyExists = true,
            IsOwnedByAdmin = false,
            IsActiveManager = true,
            PropertyOwner = new PropertyOwnerReadModel(1, 8, "Other Admin", "Admin")
        };
        var service = new PropertyManagerAssignmentService(repository);

        await Assert.ThrowsAsync<PropertyManagerAssignmentAuthorizationException>(() =>
            service.AssignManagerAsync(1, 5, 7, "Admin", CancellationToken.None));

        Assert.Null(repository.AddedAssignment);
    }

    [Fact]
    public async Task Assign_explains_legacy_owner_id_that_points_to_a_manager()
    {
        var repository = new FakePropertyManagerAssignmentRepository
        {
            PropertyExists = true,
            IsOwnedByAdmin = false,
            IsActiveManager = true,
            PropertyOwner = new PropertyOwnerReadModel(1, 5, "Legacy Manager", "Manager")
        };
        var service = new PropertyManagerAssignmentService(repository);

        var exception = await Assert.ThrowsAsync<PropertyManagerAssignmentAuthorizationException>(() =>
            service.AssignManagerAsync(1, 6, 7, "Admin", CancellationToken.None));

        Assert.Contains("OwnerId=5", exception.Message);
        Assert.Contains("Legacy Manager", exception.Message);
        Assert.Contains("Manager", exception.Message);
        Assert.Contains("không bị thay đổi", exception.Message);
        Assert.Null(repository.AddedAssignment);
    }

    [Fact]
    public async Task Assign_returns_conflict_for_existing_or_concurrent_active_assignment()
    {
        var repository = new FakePropertyManagerAssignmentRepository
        {
            PropertyExists = true,
            IsOwnedByAdmin = true,
            IsActiveManager = true,
            AddResult = null
        };
        var service = new PropertyManagerAssignmentService(repository);

        await Assert.ThrowsAsync<PropertyManagerAssignmentConflictException>(() =>
            service.AssignManagerAsync(1, 5, 7, "Admin", CancellationToken.None));

        Assert.NotNull(repository.AddedAssignment);
    }

    [Fact]
    public async Task Revoke_does_not_partially_revoke_manager_with_open_work()
    {
        var repository = new FakePropertyManagerAssignmentRepository
        {
            PropertyExists = true,
            IsOwnedByAdmin = true,
            RevokeResult = RevokePropertyManagerAssignmentResult.HasOpenWork
        };
        var service = new PropertyManagerAssignmentService(repository);

        await Assert.ThrowsAsync<PropertyManagerAssignmentConflictException>(() =>
            service.RevokeManagerAsync(1, 5, 7, "Admin", CancellationToken.None));

        Assert.Equal(1, repository.RevokeCalls);
    }

    private sealed class FakePropertyManagerAssignmentRepository : IPropertyManagerAssignmentRepository
    {
        public bool PropertyExists { get; init; }
        public bool IsOwnedByAdmin { get; init; }
        public bool IsActiveManager { get; init; }
        public PropertyOwnerReadModel PropertyOwner { get; init; } = new(1, 7, "Admin", "Admin");
        public PropertyManagerAssignmentReadModel? AddResult { get; init; } = new(
            1,
            "Property",
            5,
            "Manager",
            "manager@example.test",
            true);
        public RevokePropertyManagerAssignmentResult RevokeResult { get; init; }
        public PropertyManagerAssignment? AddedAssignment { get; private set; }
        public int RevokeCalls { get; private set; }

        public Task<bool> PropertyExistsAsync(int propertyId, CancellationToken cancellationToken)
        {
            return Task.FromResult(PropertyExists);
        }

        public Task<bool> IsOwnedByAdminAsync(int propertyId, int adminId, CancellationToken cancellationToken)
        {
            return Task.FromResult(IsOwnedByAdmin);
        }

        public Task<PropertyOwnerReadModel?> GetPropertyOwnerAsync(
            int propertyId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<PropertyOwnerReadModel?>(PropertyOwner);
        }

        public Task<bool> IsActiveManagerAsync(int managerId, CancellationToken cancellationToken)
        {
            return Task.FromResult(IsActiveManager);
        }

        public Task<PropertyManagerAssignmentReadModel?> AddIfActiveAssignmentAbsentAsync(
            PropertyManagerAssignment assignment,
            int adminId,
            CancellationToken cancellationToken)
        {
            AddedAssignment = assignment;
            return Task.FromResult(AddResult);
        }

        public Task<List<PropertyManagerAssignmentReadModel>> GetByPropertyIdAsync(
            int propertyId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new List<PropertyManagerAssignmentReadModel>());
        }

        public Task<List<PropertyManagerAssignmentReadModel>> GetByManagerIdAsync(
            int managerId,
            bool requireActiveManager,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new List<PropertyManagerAssignmentReadModel>());
        }

        public Task<List<PropertySummaryReadModel>> GetManagedPropertiesAsync(
            int managerId,
            bool requireActiveManager,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new List<PropertySummaryReadModel>());
        }

        public Task<RevokePropertyManagerAssignmentResult> RevokeActiveAsync(
            int propertyId,
            int managerId,
            int adminId,
            CancellationToken cancellationToken)
        {
            RevokeCalls++;
            return Task.FromResult(RevokeResult);
        }
    }
}
