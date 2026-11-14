using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.Services;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using Xunit;

namespace PRN232.Application.Tests;

public class ViewingScheduleServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateAsync_stores_pending_request_in_utc_and_notifies_active_admins()
    {
        var repository = new FakeViewingRepository();
        var vietnamTime = new DateTimeOffset(2026, 10, 29, 16, 0, 0, TimeSpan.FromHours(7));

        var created = await CreateService(repository).CreateAsync(new CreateViewingScheduleDto(5, vietnamTime), 42);

        var (viewing, notifications) = Assert.Single(repository.Added);
        Assert.Equal((5, 42, ViewingStatuses.Pending), (viewing.RoomId, viewing.CustomerId, viewing.Status));
        Assert.Equal(new DateTime(2026, 10, 29, 9, 0, 0, DateTimeKind.Utc), viewing.ScheduledAt);
        Assert.Equal(DateTimeKind.Utc, viewing.ScheduledAt!.Value.Kind);
        Assert.Equal(new[] { 9, 11 }, notifications.Select(notification => notification.UserId).Order().ToArray());
        Assert.All(notifications, notification =>
        {
            Assert.Equal((NotificationTypes.Viewing, false, 1), (notification.Type, notification.IsRead, notification.RelatedEntityId));
            Assert.Contains("A-101", notification.Title);
        });
        Assert.Equal(ViewingStatuses.Pending, created.Status);
    }

    [Fact]
    public async Task CreateAsync_accepts_request_where_owner_decides_the_time()
    {
        var repository = new FakeViewingRepository();

        await CreateService(repository).CreateAsync(new CreateViewingScheduleDto(5, null), 42);

        Assert.Null(Assert.Single(repository.Added).Viewing.ScheduledAt);
    }

    [Fact]
    public async Task CreateAsync_accepts_property_and_room_type_without_selecting_a_physical_room()
    {
        var repository = new FakeViewingRepository();

        var created = await CreateService(repository).CreateAsync(
            new CreateViewingScheduleDto(null, Now.AddDays(1), 1, 3),
            42);

        var added = Assert.Single(repository.Added).Viewing;
        Assert.Null(added.RoomId);
        Assert.Equal((1, 3, ViewingStatuses.Pending), (added.PropertyId, added.RoomTypeId, added.Status));
        Assert.Null(created.RoomId);
        Assert.Contains(repository.Added.Single().Notifications,
            notification => notification.Body?.Contains("Sunrise", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task Assigned_manager_can_list_and_select_a_room_for_an_approved_viewing()
    {
        var repository = new FakeViewingRepository
        {
            Viewing = SampleViewing(customerId: 10, status: ViewingStatuses.Approved) with
            {
                AssignedTo = 7,
                RoomId = null,
                RoomCode = null
            }
        };
        var service = CreateService(repository);

        var availableRooms = await service.GetAvailableRoomsAsync(1, 7, RoleNames.Manager);
        var availableRoom = Assert.Single(availableRooms);
        Assert.Equal((5, "A-101"), (availableRoom.RoomId, availableRoom.RoomCode));

        var selected = await service.SelectRoomAsync(1, new SelectViewingRoomDto(5), 7, RoleNames.Manager);

        Assert.Equal((5, "A-101"), (selected.RoomId, selected.RoomCode));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public async Task CreateAsync_rejects_time_that_is_not_in_the_future(int minutesFromNow)
    {
        var repository = new FakeViewingRepository();

        await Assert.ThrowsAsync<ViewingBusinessRuleException>(() => CreateService(repository)
            .CreateAsync(new CreateViewingScheduleDto(5, Now.AddMinutes(minutesFromNow)), 42));
        Assert.Empty(repository.Added);
    }

    [Fact]
    public async Task CreateAsync_rejects_missing_room()
    {
        await Assert.ThrowsAsync<ViewingNotFoundException>(() => CreateService(new FakeViewingRepository())
            .CreateAsync(new CreateViewingScheduleDto(99, null), 42));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task CreateAsync_rejects_unavailable_room_or_duplicate_open_request(bool rentable, bool hasOpenRequest)
    {
        var repository = new FakeViewingRepository { RoomIsRentable = rentable, HasOpenRequest = hasOpenRequest };

        await Assert.ThrowsAsync<ViewingConflictException>(() => CreateService(repository)
            .CreateAsync(new CreateViewingScheduleDto(5, null), 42));
        Assert.Empty(repository.Added);
    }

    [Theory]
    [InlineData(RoleNames.Customer, null, 42, null)]
    [InlineData(RoleNames.Manager, ViewingStatuses.Pending, null, 42)]
    [InlineData(RoleNames.Admin, ViewingStatuses.Approved, null, null)]
    public async Task GetAllAsync_scopes_list_by_role_and_passes_status(
        string role, string? status, int? expectedCustomerId, int? expectedOwnerId)
    {
        var repository = new FakeViewingRepository();

        await CreateService(repository).GetAllAsync(42, role, status);

        Assert.Equal((expectedCustomerId, expectedOwnerId), repository.LastScope);
        Assert.Equal(status, repository.LastStatus);
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("Cancelled")]
    public async Task GetAllAsync_rejects_unknown_status(string status)
    {
        await Assert.ThrowsAsync<ViewingBusinessRuleException>(() =>
            CreateService(new FakeViewingRepository()).GetAllAsync(42, RoleNames.Admin, status));
    }

    [Theory]
    [InlineData(RoleNames.Customer)]
    [InlineData(RoleNames.Manager)]
    public async Task GetByIdAsync_denies_existing_request_outside_scope(string role)
    {
        var repository = new FakeViewingRepository { Viewing = SampleViewing(customerId: 10) };

        await Assert.ThrowsAsync<ViewingAuthorizationException>(() =>
            CreateService(repository).GetByIdAsync(1, 42, role));
    }

    [Fact]
    public async Task ApproveAsync_sets_manager_assignment_and_notifies_manager_and_customer()
    {
        var repository = new FakeViewingRepository
        {
            Viewing = SampleViewing(customerId: 10) with { ScheduledAt = null }
        };

        var approved = await CreateService(repository)
            .ApproveAsync(1, new ApproveViewingScheduleDto(Now.AddDays(1), 7, "Use the side entrance."), 42, RoleNames.Admin);

        var decision = Assert.Single(repository.Decisions);
        Assert.Equal((1, ViewingStatuses.Approved, (int?)7, "Use the side entrance.", (DateTime?)Now.AddDays(1).UtcDateTime),
            (decision.ViewingId, decision.Status, decision.AssignedTo, decision.ManagerNote, decision.ScheduledAtUtc));
        Assert.Equal(new[] { 7, 10 }, decision.Notifications.Select(notification => notification.UserId).Order().ToArray());
        Assert.All(decision.Notifications, notification => Assert.Equal(1, notification.RelatedEntityId));
        Assert.Contains("chấp thuận", Assert.Single(decision.Notifications, notification => notification.UserId == 10).Title);
        Assert.Equal(ViewingStatuses.Approved, approved.Status);
    }

    [Fact]
    public async Task ApproveAsync_shows_confirmed_time_in_vietnam_time_in_notifications()
    {
        var repository = new FakeViewingRepository
        {
            Viewing = SampleViewing(customerId: 10) with { ScheduledAt = null }
        };

        // 12:00 UTC on 29/10 is 19:00 in Vietnam (UTC+7).
        await CreateService(repository)
            .ApproveAsync(1, new ApproveViewingScheduleDto(Now.AddDays(1), 7, null), 42, RoleNames.Admin);

        Assert.All(Assert.Single(repository.Decisions).Notifications, notification =>
        {
            Assert.Contains("19:00 29/10/2026", notification.Body);
            Assert.DoesNotContain("T12:00", notification.Body);
        });
    }

    [Fact]
    public async Task ApproveAsync_rejects_manager_without_active_property_assignment()
    {
        var repository = new FakeViewingRepository
        {
            Viewing = SampleViewing(customerId: 10),
            ManagerHasActivePropertyAssignment = false
        };

        await Assert.ThrowsAsync<ViewingBusinessRuleException>(() => CreateService(repository)
            .ApproveAsync(1, new ApproveViewingScheduleDto(Now.AddDays(1), 7, null), 42, RoleNames.Admin));

        Assert.Empty(repository.Decisions);
    }

    [Fact]
    public async Task ApproveAsync_keeps_customer_time_when_no_new_time_is_given()
    {
        var customerTime = Now.AddHours(3).UtcDateTime;
        var repository = new FakeViewingRepository { Viewing = SampleViewing(customerId: 10) with { ScheduledAt = customerTime } };

        var approved = await CreateService(repository)
            .ApproveAsync(1, new ApproveViewingScheduleDto(null, 7, null), 42, RoleNames.Admin);

        Assert.Equal(customerTime, Assert.Single(repository.Decisions).ScheduledAtUtc);
        Assert.Null(Assert.Single(repository.Decisions).ManagerNote);
        Assert.Equal(customerTime, approved.ScheduledAt);
    }

    [Fact]
    public async Task ApproveAsync_trims_a_valid_500_character_note()
    {
        var note = string.Concat(Enumerable.Repeat("😀", 500));
        var repository = new FakeViewingRepository { Viewing = SampleViewing(customerId: 10) };

        await CreateService(repository).ApproveAsync(
            1,
            new ApproveViewingScheduleDto(Now.AddDays(1), 7, $"  {note}  "),
            42,
            RoleNames.Admin);

        Assert.Equal(note, Assert.Single(repository.Decisions).ManagerNote);
    }

    [Fact]
    public async Task ApproveAsync_rejects_a_note_over_500_characters()
    {
        var repository = new FakeViewingRepository { Viewing = SampleViewing(customerId: 10) };

        await Assert.ThrowsAsync<ViewingBusinessRuleException>(() => CreateService(repository).ApproveAsync(
            1,
            new ApproveViewingScheduleDto(Now.AddDays(1), 7, new string('x', 501)),
            42,
            RoleNames.Admin));

        Assert.Empty(repository.Decisions);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-60)]
    public async Task ApproveAsync_requires_a_future_time(int? customerMinutesFromNow)
    {
        var customerTime = customerMinutesFromNow is int minutes ? Now.AddMinutes(minutes).UtcDateTime : (DateTime?)null;
        var repository = new FakeViewingRepository { Viewing = SampleViewing(customerId: 10) with { ScheduledAt = customerTime } };

        await Assert.ThrowsAsync<ViewingBusinessRuleException>(() => CreateService(repository)
            .ApproveAsync(1, new ApproveViewingScheduleDto(null, 7, null), 42, RoleNames.Admin));
        Assert.Empty(repository.Decisions);
    }

    [Fact]
    public async Task RejectAsync_keeps_time_and_notifies_customer_with_reason()
    {
        var repository = new FakeViewingRepository { Viewing = SampleViewing(customerId: 10) };

        var rejected = await CreateService(repository).RejectAsync(1, new RejectViewingScheduleDto("Room unavailable."), 42, RoleNames.Admin);

        var decision = Assert.Single(repository.Decisions);
        Assert.Equal((ViewingStatuses.Rejected, (DateTime?)null, "Room unavailable."),
            (decision.Status, decision.ScheduledAtUtc, decision.ManagerNote));
        Assert.Equal(10, Assert.Single(decision.Notifications).UserId);
        Assert.Equal(ViewingStatuses.Rejected, rejected.Status);
    }

    [Theory]
    [InlineData(ViewingStatuses.Approved)]
    [InlineData(ViewingStatuses.Rejected)]
    public async Task Handling_a_request_that_is_no_longer_pending_is_a_conflict(string status)
    {
        var repository = new FakeViewingRepository { Viewing = SampleViewing(customerId: 10, status: status) };

        await Assert.ThrowsAsync<ViewingConflictException>(() => CreateService(repository)
            .RejectAsync(1, new RejectViewingScheduleDto("Not available."), 42, RoleNames.Admin));
        Assert.Empty(repository.Decisions);
    }

    [Fact]
    public async Task Customers_and_managers_outside_scope_cannot_handle_requests()
    {
        var repository = new FakeViewingRepository { Viewing = SampleViewing(customerId: 42) };

        await Assert.ThrowsAsync<ViewingAuthorizationException>(() =>
            CreateService(repository).RejectAsync(1, new RejectViewingScheduleDto("Reason"), 42, RoleNames.Customer));
        await Assert.ThrowsAsync<ViewingAuthorizationException>(() =>
            CreateService(repository).RejectAsync(1, new RejectViewingScheduleDto("Reason"), 8, RoleNames.Manager));
        Assert.Empty(repository.Decisions);
    }

    [Fact]
    public async Task Handling_reports_conflict_when_another_manager_handled_it_first()
    {
        var repository = new FakeViewingRepository { Viewing = SampleViewing(customerId: 10), DecideSucceeds = false };

        await Assert.ThrowsAsync<ViewingConflictException>(() => CreateService(repository)
            .RejectAsync(1, new RejectViewingScheduleDto("Reason"), 42, RoleNames.Admin));
    }

    private static ViewingScheduleService CreateService(FakeViewingRepository repository) =>
        new(repository, new FixedTimeProvider(Now), new FakeNotificationService());

    private static ViewingScheduleDto SampleViewing(int customerId, string status = ViewingStatuses.Pending) =>
        new(1, 5, "A-101", 1, "Sunrise", 3, "Premium", 3_000_000m, customerId, "Customer", "0900000000",
            Now.AddDays(1).UtcDateTime, status, null, null, null, null, null, null);

    private sealed class FakeViewingRepository : IViewingScheduleRepository
    {
        public ViewingRoomReadModel? Room { get; init; } = new(5, "A-101", 1, 3, 7);
        public ViewingManagerReadModel? Manager { get; init; } = new(7, "Manager A", "0911000000", RoleNames.Manager, true);
        public bool RoomIsRentable { get; init; } = true;
        public bool HasOpenRequest
        {
            get; init;
        }
        public bool DecideSucceeds { get; init; } = true;
        public bool ManagerHasActivePropertyAssignment { get; init; } = true;
        public ViewingScheduleDto? Viewing
        {
            get; set;
        }
        private ViewingScheduleReadModel? ViewingReadModel => Viewing is null ? null : new ViewingScheduleReadModel(
            Viewing.ViewingId, Viewing.RoomId, Viewing.RoomCode, Viewing.PropertyId, Viewing.PropertyName,
            Viewing.RoomTypeId, Viewing.RoomTypeName, Viewing.CurrentMonthlyRent,
            Viewing.CustomerId, Viewing.CustomerName, Viewing.CustomerPhone, Viewing.ScheduledAt, Viewing.Status,
            Viewing.AssignedTo, Viewing.AssignedManagerName, Viewing.AssignedManagerPhone, Viewing.ManagerNote,
            Viewing.RespondedBy, Viewing.RespondedAt);
        public List<(ViewingSchedules Viewing, IReadOnlyCollection<Notifications> Notifications)> Added { get; } = [];
        public List<(int ViewingId, string Status, int? AssignedTo, string? ManagerNote, int RespondedBy,
            DateTime RespondedAtUtc, DateTime? ScheduledAtUtc, IReadOnlyCollection<Notifications> Notifications)> Decisions { get; } = [];
        public (int? CustomerId, int? AssignedTo) LastScope
        {
            get; private set;
        }
        public string? LastStatus
        {
            get; private set;
        }

        public Task<ViewingRoomReadModel?> GetRoomAsync(int roomId) =>
            Task.FromResult(Room?.RoomId == roomId ? Room : null);

        public Task<ViewingPropertyRoomTypeReadModel?> GetActivePropertyRoomTypeAsync(int propertyId, int roomTypeId) =>
            Task.FromResult(propertyId == 1 && roomTypeId == 3
                ? new ViewingPropertyRoomTypeReadModel(1, "Sunrise", 3, "Premium", 3_000_000m)
                : null);

        public Task<ViewingManagerReadModel?> GetManagerAsync(int userId) =>
            Task.FromResult(Manager?.UserId == userId ? Manager : null);

        public Task<bool> HasActiveManagerAssignmentAsync(int propertyId, int managerId) =>
            Task.FromResult(ManagerHasActivePropertyAssignment && propertyId == 1 && managerId == 7);

        public Task<bool> IsRoomRentableAsync(int roomId, DateTimeOffset asOfUtc) => Task.FromResult(RoomIsRentable);

        public Task<bool> HasOpenRequestAsync(int customerId, int roomId, DateTimeOffset asOfUtc) =>
            Task.FromResult(HasOpenRequest);

        public Task<bool> HasOpenRequestForRoomTypeAsync(int customerId, int propertyId, int roomTypeId, DateTimeOffset asOfUtc) =>
            Task.FromResult(HasOpenRequest);

        public Task<List<ViewingAvailableRoomReadModel>> GetAvailableRoomsAsync(int propertyId, int roomTypeId, DateTimeOffset asOfUtc) =>
            Task.FromResult(new List<ViewingAvailableRoomReadModel> { new(5, "A-101", 24m, "Available") });

        public Task<bool> SelectRoomAsync(int viewingId, int managerId, int roomId, DateTimeOffset asOfUtc, Notifications notification)
        {
            if (!ManagerHasActivePropertyAssignment || !RoomIsRentable || Viewing is null || Viewing.RoomId.HasValue)
            {
                return Task.FromResult(false);
            }
            Viewing = Viewing with { RoomId = roomId, RoomCode = Room?.RoomCode };
            return Task.FromResult(true);
        }

        public Task AddAsync(ViewingSchedules viewing, Func<int, IReadOnlyCollection<Notifications>> notificationFactory)
        {
            viewing.ViewingId = 1;
            var notifications = notificationFactory(viewing.ViewingId);
            Added.Add((viewing, notifications));
            Viewing = new ViewingScheduleDto(1, viewing.RoomId, Room?.RoomCode, 1, "Sunrise", 3, "Premium", 3_000_000m,
                viewing.CustomerId, "Customer", "0900000000", viewing.ScheduledAt, viewing.Status,
                null, null, null, null, null, null);
            return Task.CompletedTask;
        }

        public Task<List<ViewingScheduleReadModel>> GetAllAsync(int? customerId, int? assignedTo, string? status)
        {
            LastScope = (customerId, assignedTo);
            LastStatus = status;
            var visible = Viewing is not null &&
                (customerId is null || Viewing.CustomerId == customerId) &&
                (assignedTo is null || Viewing.AssignedTo == assignedTo) &&
                (status is null || Viewing.Status == status);
            return Task.FromResult(visible ? new List<ViewingScheduleReadModel> { ViewingReadModel! } : []);
        }

        public Task<ViewingScheduleReadModel?> GetByIdAsync(int viewingId, int? customerId, int? assignedTo)
        {
            var visible = Viewing is not null && Viewing.ViewingId == viewingId &&
                (customerId is null || Viewing.CustomerId == customerId) &&
                (assignedTo is null || Viewing.AssignedTo == assignedTo);
            return Task.FromResult(visible ? ViewingReadModel : null);
        }

        public Task<bool> ExistsAsync(int viewingId) => Task.FromResult(Viewing?.ViewingId == viewingId);

        public Task<bool> DecideAsync(
            int viewingId,
            string status,
            int? assignedTo,
            string? managerNote,
            int respondedBy,
            DateTime respondedAtUtc,
            DateTime? scheduledAtUtc,
            IReadOnlyCollection<Notifications> notifications)
        {
            if (!DecideSucceeds)
            {
                return Task.FromResult(false);
            }

            Decisions.Add((viewingId, status, assignedTo, managerNote, respondedBy, respondedAtUtc, scheduledAtUtc, notifications));
            Viewing = Viewing! with
            {
                Status = status,
                ScheduledAt = scheduledAtUtc ?? Viewing.ScheduledAt,
                AssignedTo = assignedTo,
                AssignedManagerName = assignedTo is null ? null : Manager?.FullName,
                AssignedManagerPhone = assignedTo is null ? null : Manager?.PhoneNumber,
                ManagerNote = managerNote,
                RespondedBy = respondedBy,
                RespondedAt = respondedAtUtc
            };
            return Task.FromResult(true);
        }
    }

    private sealed class FakeNotificationService : INotificationService
    {
        public Task<List<NotificationDto>> GetAllAsync(int userId, bool? isRead, string? type) => throw new NotImplementedException();
        public Task UpdateReadStateAsync(int id, int userId, bool? isRead) => throw new NotImplementedException();
        public Task<List<int>> GetActiveAdminUserIdsAsync() => Task.FromResult(new List<int> { 9, 11 });
        public IReadOnlyList<Notifications> BuildMaintenanceNotifications(IEnumerable<int> userIds, string title) => throw new NotImplementedException();
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
