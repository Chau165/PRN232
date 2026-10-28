using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using Xunit;

namespace PRN232.Application.Tests;

public class ViewingScheduleServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateAsync_stores_pending_request_in_utc_and_notifies_owner()
    {
        var repository = new FakeViewingRepository();
        var vietnamTime = new DateTimeOffset(2026, 10, 29, 16, 0, 0, TimeSpan.FromHours(7));

        var created = await CreateService(repository).CreateAsync(new CreateViewingScheduleDto(5, vietnamTime), 42);

        var (viewing, notification) = Assert.Single(repository.Added);
        Assert.Equal((5, 42, ViewingStatuses.Pending), (viewing.RoomId, viewing.CustomerId, viewing.Status));
        Assert.Equal(new DateTime(2026, 10, 29, 9, 0, 0, DateTimeKind.Utc), viewing.ScheduledAt);
        Assert.Equal(DateTimeKind.Utc, viewing.ScheduledAt!.Value.Kind);
        Assert.Equal((7, NotificationTypes.Viewing, false), (notification.UserId, notification.Type, notification.IsRead));
        Assert.Contains("A-101", notification.Title);
        Assert.Equal(ViewingStatuses.Pending, created.Status);
    }

    [Fact]
    public async Task CreateAsync_accepts_request_where_owner_decides_the_time()
    {
        var repository = new FakeViewingRepository();

        await CreateService(repository).CreateAsync(new CreateViewingScheduleDto(5, null), 42);

        Assert.Null(Assert.Single(repository.Added).Viewing.ScheduledAt);
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
        var repository = new FakeViewingRepository { Viewing = SampleViewing(customerId: 10), OwnerId = 7 };

        await Assert.ThrowsAsync<ViewingAuthorizationException>(() =>
            CreateService(repository).GetByIdAsync(1, 42, role));
    }

    [Fact]
    public async Task ApproveAsync_sets_owner_chosen_time_and_notifies_customer()
    {
        var repository = new FakeViewingRepository
        {
            Viewing = SampleViewing(customerId: 10) with { ScheduledAt = null }, OwnerId = 42
        };

        var approved = await CreateService(repository)
            .ApproveAsync(1, new ApproveViewingScheduleDto(Now.AddDays(1)), 42, RoleNames.Manager);

        var decision = Assert.Single(repository.Decisions);
        Assert.Equal((1, ViewingStatuses.Approved, (DateTime?)Now.AddDays(1).UtcDateTime),
            (decision.ViewingId, decision.Status, decision.ScheduledAtUtc));
        Assert.Equal((10, NotificationTypes.Viewing), (decision.Notification.UserId, decision.Notification.Type));
        Assert.Contains("chấp thuận", decision.Notification.Title);
        Assert.Equal(ViewingStatuses.Approved, approved.Status);
    }

    [Fact]
    public async Task ApproveAsync_keeps_customer_time_when_no_new_time_is_given()
    {
        var customerTime = Now.AddHours(3).UtcDateTime;
        var repository = new FakeViewingRepository { Viewing = SampleViewing(customerId: 10) with { ScheduledAt = customerTime } };

        var approved = await CreateService(repository)
            .ApproveAsync(1, new ApproveViewingScheduleDto(null), 42, RoleNames.Admin);

        Assert.Null(Assert.Single(repository.Decisions).ScheduledAtUtc);
        Assert.Equal(customerTime, approved.ScheduledAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-60)]
    public async Task ApproveAsync_requires_a_future_time(int? customerMinutesFromNow)
    {
        var customerTime = customerMinutesFromNow is int minutes ? Now.AddMinutes(minutes).UtcDateTime : (DateTime?)null;
        var repository = new FakeViewingRepository { Viewing = SampleViewing(customerId: 10) with { ScheduledAt = customerTime } };

        await Assert.ThrowsAsync<ViewingBusinessRuleException>(() => CreateService(repository)
            .ApproveAsync(1, new ApproveViewingScheduleDto(null), 42, RoleNames.Admin));
        Assert.Empty(repository.Decisions);
    }

    [Fact]
    public async Task RejectAsync_keeps_time_and_notifies_customer()
    {
        var repository = new FakeViewingRepository { Viewing = SampleViewing(customerId: 10) };

        var rejected = await CreateService(repository).RejectAsync(1, 42, RoleNames.Admin);

        var decision = Assert.Single(repository.Decisions);
        Assert.Equal((ViewingStatuses.Rejected, (DateTime?)null), (decision.Status, decision.ScheduledAtUtc));
        Assert.Equal(10, decision.Notification.UserId);
        Assert.Equal(ViewingStatuses.Rejected, rejected.Status);
    }

    [Theory]
    [InlineData(ViewingStatuses.Approved)]
    [InlineData(ViewingStatuses.Rejected)]
    public async Task Handling_a_request_that_is_no_longer_pending_is_a_conflict(string status)
    {
        var repository = new FakeViewingRepository { Viewing = SampleViewing(customerId: 10, status: status) };

        await Assert.ThrowsAsync<ViewingConflictException>(() => CreateService(repository).RejectAsync(1, 42, RoleNames.Admin));
        Assert.Empty(repository.Decisions);
    }

    [Fact]
    public async Task Customers_and_managers_outside_scope_cannot_handle_requests()
    {
        var repository = new FakeViewingRepository { Viewing = SampleViewing(customerId: 42), OwnerId = 7 };

        await Assert.ThrowsAsync<ViewingAuthorizationException>(() =>
            CreateService(repository).RejectAsync(1, 42, RoleNames.Customer));
        await Assert.ThrowsAsync<ViewingAuthorizationException>(() =>
            CreateService(repository).RejectAsync(1, 8, RoleNames.Manager));
        Assert.Empty(repository.Decisions);
    }

    [Fact]
    public async Task Handling_reports_conflict_when_another_manager_handled_it_first()
    {
        var repository = new FakeViewingRepository { Viewing = SampleViewing(customerId: 10), DecideSucceeds = false };

        await Assert.ThrowsAsync<ViewingConflictException>(() => CreateService(repository).RejectAsync(1, 42, RoleNames.Admin));
    }

    private static ViewingScheduleService CreateService(FakeViewingRepository repository) =>
        new(repository, new FixedTimeProvider(Now));

    private static ViewingScheduleDto SampleViewing(int customerId, string status = ViewingStatuses.Pending) =>
        new(1, 5, "A-101", 1, "Sunrise", customerId, "Customer", "0900000000", Now.AddDays(1).UtcDateTime, status);

    private sealed class FakeViewingRepository : IViewingScheduleRepository
    {
        public ViewingRoomDto? Room { get; init; } = new(5, "A-101", 7);
        public bool RoomIsRentable { get; init; } = true;
        public bool HasOpenRequest { get; init; }
        public bool DecideSucceeds { get; init; } = true;
        public ViewingScheduleDto? Viewing { get; set; }
        public int OwnerId { get; init; } = 7;
        public List<(ViewingSchedules Viewing, Notifications Notification)> Added { get; } = [];
        public List<(int ViewingId, string Status, DateTime? ScheduledAtUtc, Notifications Notification)> Decisions { get; } = [];
        public (int? CustomerId, int? OwnerId) LastScope { get; private set; }
        public string? LastStatus { get; private set; }

        public Task<ViewingRoomDto?> GetRoomAsync(int roomId) =>
            Task.FromResult(Room?.RoomId == roomId ? Room : null);

        public Task<bool> IsRoomRentableAsync(int roomId, DateTimeOffset asOfUtc) => Task.FromResult(RoomIsRentable);

        public Task<bool> HasOpenRequestAsync(int customerId, int roomId, DateTimeOffset asOfUtc) =>
            Task.FromResult(HasOpenRequest);

        public Task AddAsync(ViewingSchedules viewing, Notifications ownerNotification)
        {
            viewing.ViewingId = 1;
            Added.Add((viewing, ownerNotification));
            Viewing = new ViewingScheduleDto(1, viewing.RoomId, Room!.RoomCode, 1, "Sunrise", viewing.CustomerId,
                "Customer", "0900000000", viewing.ScheduledAt, viewing.Status);
            return Task.CompletedTask;
        }

        public Task<List<ViewingScheduleDto>> GetAllAsync(int? customerId, int? ownerId, string? status)
        {
            LastScope = (customerId, ownerId);
            LastStatus = status;
            return Task.FromResult(Viewing is null ? new List<ViewingScheduleDto>() : [Viewing]);
        }

        public Task<ViewingScheduleDto?> GetByIdAsync(int viewingId, int? customerId, int? ownerId)
        {
            var visible = Viewing is not null && Viewing.ViewingId == viewingId &&
                (customerId is null || Viewing.CustomerId == customerId) &&
                (ownerId is null || OwnerId == ownerId);
            return Task.FromResult(visible ? Viewing : null);
        }

        public Task<bool> ExistsAsync(int viewingId) => Task.FromResult(Viewing?.ViewingId == viewingId);

        public Task<bool> DecideAsync(
            int viewingId, string status, DateTime? scheduledAtUtc, Notifications customerNotification)
        {
            if (!DecideSucceeds)
            {
                return Task.FromResult(false);
            }

            Decisions.Add((viewingId, status, scheduledAtUtc, customerNotification));
            Viewing = Viewing! with { Status = status, ScheduledAt = scheduledAtUtc ?? Viewing.ScheduledAt };
            return Task.FromResult(true);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
