using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;
using PRN232.Domain.Entities;
using Xunit;

namespace PRN232.Application.Tests;

public class MaintenanceServiceTests
{
    private static readonly DateOnly Today = new(2026, 10, 8);

    [Fact]
    public async Task CreateAsync_creates_pending_request_for_valid_contract_and_equipment()
    {
        var repository = new FakeMaintenanceRepository
        {
            Contract = ValidContract(start: Today, end: Today)
        };
        repository.EquipmentRooms[5] = 12;
        var service = CreateService(repository);

        var created = await service.CreateAsync(new CreateMaintenanceRequestDto(10, 5, "AC is broken"));

        Assert.Equal("Pending", created.Status);
        Assert.Null(created.AssignedTo);
        Assert.Equal(5, created.EquipmentId);
        Assert.Equal(1, repository.AddCount);
    }

    [Fact]
    public async Task CreateAsync_rejects_missing_contract()
    {
        var service = CreateService(new FakeMaintenanceRepository());

        await Assert.ThrowsAsync<MaintenanceNotFoundException>(() =>
            service.CreateAsync(new CreateMaintenanceRequestDto(10, null, "broken")));
    }

    [Theory]
    [InlineData(2026, 10, 9, 2026, 12, 1)]
    [InlineData(2026, 1, 1, 2026, 10, 7)]
    public async Task CreateAsync_rejects_contract_outside_date_range(
        int startYear, int startMonth, int startDay, int endYear, int endMonth, int endDay)
    {
        var repository = new FakeMaintenanceRepository
        {
            Contract = ValidContract(new DateOnly(startYear, startMonth, startDay), new DateOnly(endYear, endMonth, endDay))
        };

        await Assert.ThrowsAsync<MaintenanceContractException>(() =>
            CreateService(repository).CreateAsync(new CreateMaintenanceRequestDto(10, null, "broken")));
        Assert.Equal(0, repository.AddCount);
    }

    [Fact]
    public async Task CreateAsync_rejects_missing_equipment()
    {
        var repository = new FakeMaintenanceRepository { Contract = ValidContract(Today, Today) };

        await Assert.ThrowsAsync<MaintenanceNotFoundException>(() =>
            CreateService(repository).CreateAsync(new CreateMaintenanceRequestDto(10, 5, "broken")));
    }

    [Fact]
    public async Task CreateAsync_rejects_equipment_in_another_room()
    {
        var repository = new FakeMaintenanceRepository { Contract = ValidContract(Today, Today) };
        repository.EquipmentRooms[5] = 99;

        await Assert.ThrowsAsync<MaintenanceEquipmentException>(() =>
            CreateService(repository).CreateAsync(new CreateMaintenanceRequestDto(10, 5, "broken")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_rejects_blank_description(string description)
    {
        var repository = new FakeMaintenanceRepository { Contract = ValidContract(Today, Today) };

        await Assert.ThrowsAsync<MaintenanceBusinessRuleException>(() =>
            CreateService(repository).CreateAsync(new CreateMaintenanceRequestDto(10, null, description)));
    }

    [Fact]
    public async Task GetAllAsync_forwards_customer_status_and_assignee_filters()
    {
        var repository = new FakeMaintenanceRepository();
        var service = CreateService(repository);

        await service.GetAllAsync(42, "Pending", 7);

        Assert.Equal((42, "Pending", 7), repository.LastFilters);
    }

    [Fact]
    public async Task GetByIdAsync_returns_null_for_missing_request()
    {
        var result = await CreateService(new FakeMaintenanceRepository()).GetByIdAsync(404);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_returns_projected_detail_for_existing_request()
    {
        var repository = ValidRequest("Pending", assignedTo: null);

        var result = await CreateService(repository).GetByIdAsync(1);

        Assert.NotNull(result);
        Assert.Equal((1, 10, 42, 12, "A-12", (int?)5),
            (result.RequestId, result.ContractId, result.CustomerId, result.RoomId, result.RoomCode, result.EquipmentId));
    }

    [Fact]
    public async Task GetLogsAsync_returns_ordered_logs_for_existing_request()
    {
        var repository = ValidRequest("Completed", assignedTo: 7);
        repository.Logs.Add(new MaintenanceLogDto(2, 1, "Approved", "InProgress", null, null,
            new DateTime(2026, 10, 8, 11, 0, 0, DateTimeKind.Utc)));
        repository.Logs.Add(new MaintenanceLogDto(1, 1, "Pending", "Approved", null, null,
            new DateTime(2026, 10, 8, 11, 0, 0, DateTimeKind.Utc)));

        var logs = await CreateService(repository).GetLogsAsync(1);

        Assert.Equal(new[] { 1, 2 }, logs!.Select(log => log.LogId));
    }

    [Fact]
    public async Task GetLogsAsync_returns_null_for_missing_request()
    {
        var logs = await CreateService(new FakeMaintenanceRepository()).GetLogsAsync(404);

        Assert.Null(logs);
    }

    [Fact]
    public async Task UpdateAsync_approves_and_assigns_atomically_with_one_utc_log()
    {
        var repository = ValidRequest("Pending", assignedTo: null);
        repository.Contract = ValidContract(Today, Today);
        repository.EquipmentRooms[5] = 12;
        repository.ActiveUsers[7] = true;
        var service = CreateService(repository);

        var updated = await service.UpdateAsync(1, new UpdateMaintenanceRequestDto("Approved", 7), null);

        Assert.Equal("Approved", updated!.Status);
        Assert.Equal(7, updated.AssignedTo);
        Assert.Single(repository.Logs);
        var log = Assert.Single(repository.Logs);
        Assert.Equal(("Pending", "Approved", null), (log.OldStatus, log.NewStatus, log.ChangedBy));
        Assert.Equal(DateTimeKind.Utc, log.ChangedAt.Kind);
        Assert.Equal(1, repository.AtomicUpdateCount);
    }

    [Fact]
    public async Task UpdateAsync_rejects_approval_without_assignee()
    {
        var repository = ValidRequest("Pending", assignedTo: null);
        repository.Contract = ValidContract(Today, Today);

        await Assert.ThrowsAsync<MaintenanceAssigneeException>(() =>
            CreateService(repository).UpdateAsync(1, new UpdateMaintenanceRequestDto("Approved", null), null));
        Assert.Empty(repository.Logs);
    }

    [Fact]
    public async Task UpdateAsync_rejects_missing_assignee()
    {
        var repository = ValidRequest("Pending", assignedTo: null);
        repository.Contract = ValidContract(Today, Today);

        await Assert.ThrowsAsync<MaintenanceNotFoundException>(() =>
            CreateService(repository).UpdateAsync(1, new UpdateMaintenanceRequestDto("Approved", 7), null));
    }

    [Fact]
    public async Task UpdateAsync_rejects_assignee_that_disappeared_before_approval()
    {
        var repository = ValidRequest("Pending", assignedTo: null);
        repository.Contract = ValidContract(Today, Today);
        repository.EquipmentRooms[5] = 12;

        await Assert.ThrowsAsync<MaintenanceNotFoundException>(() =>
            CreateService(repository).UpdateAsync(1, new UpdateMaintenanceRequestDto("Approved", 7), null));
    }

    [Fact]
    public async Task UpdateAsync_rejects_inactive_assignee()
    {
        var repository = ValidRequest("Pending", assignedTo: null);
        repository.Contract = ValidContract(Today, Today);
        repository.EquipmentRooms[5] = 12;
        repository.ActiveUsers[7] = false;

        await Assert.ThrowsAsync<MaintenanceAssigneeException>(() =>
            CreateService(repository).UpdateAsync(1, new UpdateMaintenanceRequestDto("Approved", 7), null));
    }

    [Fact]
    public async Task UpdateAsync_revalidates_contract_dates_at_approval()
    {
        var repository = ValidRequest("Pending", assignedTo: null);
        repository.Contract = ValidContract(new DateOnly(2026, 1, 1), new DateOnly(2026, 10, 7));
        repository.ActiveUsers[7] = true;

        await Assert.ThrowsAsync<MaintenanceContractException>(() =>
            CreateService(repository).UpdateAsync(1, new UpdateMaintenanceRequestDto("Approved", 7), null));
        Assert.Empty(repository.Logs);
    }

    [Fact]
    public async Task UpdateAsync_rejects_equipment_room_mismatch_at_approval()
    {
        var repository = ValidRequest("Pending", assignedTo: null);
        repository.Contract = ValidContract(Today, Today);
        repository.EquipmentRooms[5] = 99;
        repository.ActiveUsers[7] = true;

        await Assert.ThrowsAsync<MaintenanceEquipmentException>(() =>
            CreateService(repository).UpdateAsync(1, new UpdateMaintenanceRequestDto("Approved", 7), null));
        Assert.Empty(repository.Logs);
    }

    [Theory]
    [InlineData("Pending", "Approved", 7)]
    [InlineData("Pending", "Rejected", null)]
    [InlineData("Approved", "InProgress", null)]
    [InlineData("InProgress", "Completed", null)]
    public async Task UpdateAsync_accepts_allowed_transitions(string oldStatus, string newStatus, int? assignee)
    {
        var repository = ValidRequest(oldStatus, assignedTo: 7);
        if (oldStatus == "Pending" && newStatus == "Approved")
        {
            repository.Contract = ValidContract(Today, Today);
            repository.EquipmentRooms[5] = 12;
            repository.ActiveUsers[7] = true;
        }

        await CreateService(repository).UpdateAsync(1, new UpdateMaintenanceRequestDto(newStatus, assignee), null);

        Assert.Equal(newStatus, repository.State!.Status);
        Assert.Single(repository.Logs);
    }

    [Theory]
    [InlineData("Pending", "Completed")]
    [InlineData("Completed", "Pending")]
    [InlineData("Rejected", "InProgress")]
    [InlineData("Completed", "Approved")]
    public async Task UpdateAsync_rejects_disallowed_transitions(string oldStatus, string newStatus)
    {
        var repository = ValidRequest(oldStatus, assignedTo: 7);

        await Assert.ThrowsAsync<MaintenanceStatusTransitionException>(() =>
            CreateService(repository).UpdateAsync(1, new UpdateMaintenanceRequestDto(newStatus, null), null));
        Assert.Empty(repository.Logs);
    }

    [Fact]
    public async Task UpdateAsync_preserves_assignment_when_later_transition_omits_it()
    {
        var repository = ValidRequest("Approved", assignedTo: 7);

        var updated = await CreateService(repository)
            .UpdateAsync(1, new UpdateMaintenanceRequestDto("InProgress", null), null);

        Assert.Equal(7, updated!.AssignedTo);
    }

    [Fact]
    public async Task UpdateAsync_validates_explicit_assignment_on_later_transition()
    {
        var repository = ValidRequest("Approved", assignedTo: 7);
        repository.ActiveUsers[8] = true;

        var updated = await CreateService(repository)
            .UpdateAsync(1, new UpdateMaintenanceRequestDto("InProgress", 8), null);

        Assert.Equal(8, updated!.AssignedTo);
    }

    [Fact]
    public async Task UpdateAsync_rejects_missing_explicit_assignment_on_later_transition()
    {
        var repository = ValidRequest("Approved", assignedTo: 7);

        await Assert.ThrowsAsync<MaintenanceNotFoundException>(() =>
            CreateService(repository).UpdateAsync(1, new UpdateMaintenanceRequestDto("InProgress", 8), null));
        Assert.Empty(repository.Logs);
    }

    [Fact]
    public async Task UpdateAsync_rejects_inactive_explicit_assignment_on_later_transition()
    {
        var repository = ValidRequest("Approved", assignedTo: 7);
        repository.ActiveUsers[8] = false;

        await Assert.ThrowsAsync<MaintenanceAssigneeException>(() =>
            CreateService(repository).UpdateAsync(1, new UpdateMaintenanceRequestDto("InProgress", 8), null));
        Assert.Empty(repository.Logs);
    }

    [Fact]
    public async Task UpdateAsync_rejects_missing_request()
    {
        var repository = new FakeMaintenanceRepository();

        var updated = await CreateService(repository)
            .UpdateAsync(404, new UpdateMaintenanceRequestDto("Approved", 7), null);

        Assert.Null(updated);
        Assert.Empty(repository.Logs);
    }

    [Fact]
    public async Task UpdateAsync_does_not_log_if_request_changed_concurrently()
    {
        var repository = ValidRequest("Approved", assignedTo: 7);
        repository.FailAtomicUpdate = true;

        await Assert.ThrowsAsync<MaintenanceStatusTransitionException>(() =>
            CreateService(repository).UpdateAsync(1, new UpdateMaintenanceRequestDto("InProgress", null), null));
        Assert.Empty(repository.Logs);
    }

    [Fact]
    public async Task UpdateAsync_rejects_non_pending_approval_and_does_not_log()
    {
        var repository = ValidRequest("Completed", assignedTo: 7);

        await Assert.ThrowsAsync<MaintenanceStatusTransitionException>(() =>
            CreateService(repository).UpdateAsync(1, new UpdateMaintenanceRequestDto("Approved", 7), null));
        Assert.Empty(repository.Logs);
    }

    private static MaintenanceService CreateService(FakeMaintenanceRepository repository) =>
        new(repository, new FixedTimeProvider(new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero)));

    private static FakeMaintenanceRepository ValidRequest(string status, int? assignedTo) => new()
    {
        State = new MaintenanceRequestStateDto(1, 10, 5, assignedTo, status)
    };

    private static MaintenanceContractDto ValidContract(DateOnly start, DateOnly end) =>
        new(10, 12, 42, start, end, "Unfinalized status");

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class FakeMaintenanceRepository : IMaintenanceRepository
    {
        private int _nextId = 1;

        public MaintenanceContractDto? Contract { get; set; }
        public MaintenanceRequestStateDto? State { get; set; }
        public Dictionary<int, int> EquipmentRooms { get; } = new();
        public Dictionary<int, bool> ActiveUsers { get; } = new();
        public List<MaintenanceLogDto> Logs { get; } = new();
        public int AddCount { get; private set; }
        public int AtomicUpdateCount { get; private set; }
        public bool FailAtomicUpdate { get; set; }
        public (int? CustomerId, string? Status, int? AssignedTo) LastFilters { get; private set; }

        public Task<List<MaintenanceRequestDto>> GetAllAsync(int? customerId, string? status, int? assignedTo)
        {
            LastFilters = (customerId, status, assignedTo);
            return Task.FromResult(new List<MaintenanceRequestDto>());
        }

        public Task<MaintenanceRequestDto?> GetByIdAsync(int id) =>
            Task.FromResult(State?.RequestId == id ? ToDto(State) : null);

        public Task<MaintenanceRequestStateDto?> GetStateByIdAsync(int id) =>
            Task.FromResult(State?.RequestId == id ? State : null);

        public Task<MaintenanceContractDto?> GetContractAsync(int contractId) =>
            Task.FromResult(Contract?.ContractId == contractId ? Contract : null);

        public Task<int?> GetEquipmentRoomIdAsync(int equipmentId) =>
            Task.FromResult(EquipmentRooms.TryGetValue(equipmentId, out var roomId) ? (int?)roomId : null);

        public Task<bool?> GetUserActiveStatusAsync(int userId) =>
            Task.FromResult(ActiveUsers.TryGetValue(userId, out var active) ? (bool?)active : null);

        public Task<MaintenanceRequests> AddAsync(MaintenanceRequests request)
        {
            request.RequestId = _nextId++;
            State = new MaintenanceRequestStateDto(request.RequestId, request.ContractId, request.EquipmentId,
                request.AssignedTo, request.Status);
            AddCount++;
            return Task.FromResult(request);
        }

        public Task SaveChangesAsync() => Task.CompletedTask;

        public Task<bool> UpdateStatusAndAddLogAsync(int requestId, string expectedStatus, string status,
            int? assignedTo, int? changedBy, DateTime changedAt)
        {
            if (FailAtomicUpdate)
            {
                return Task.FromResult(false);
            }
            if (State?.RequestId != requestId)
            {
                return Task.FromResult(false);
            }
            if (State.Status != expectedStatus)
            {
                return Task.FromResult(false);
            }

            Logs.Add(new MaintenanceLogDto(Logs.Count + 1, requestId, State.Status, status, changedBy,
                null, changedAt));
            State = State with { Status = status, AssignedTo = assignedTo ?? State.AssignedTo };
            AtomicUpdateCount++;
            return Task.FromResult(true);
        }

        public Task<List<MaintenanceLogDto>?> GetLogsAsync(int requestId) =>
            Task.FromResult(State?.RequestId == requestId ? Logs.OrderBy(x => x.ChangedAt).ThenBy(x => x.LogId).ToList() : null);

        private static MaintenanceRequestDto ToDto(MaintenanceRequestStateDto state) => new(
            state.RequestId, "broken", state.Status, state.ContractId, 42, "Customer", 12, "A-12",
            state.EquipmentId, state.EquipmentId.HasValue ? "AC" : null,
            state.EquipmentId.HasValue ? "Working" : null, state.AssignedTo,
            state.AssignedTo.HasValue ? "Staff" : null);
    }
}
