using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.Services;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using Xunit;

namespace PRN232.Application.Tests;

public class ContractServiceTests
{
    // 18:00 UTC ngày 28/10 = 01:00 sáng ngày 29/10 giờ Việt Nam.
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly VietnamToday = new(2026, 10, 29);

    [Fact]
    public async Task CreateFromBookingAsync_copies_booking_terms_and_notifies_customer()
    {
        var repository = new FakeContractRepository();

        var created = await CreateService(repository).CreateFromBookingAsync(
            new CreateContractDto(1, VietnamToday, VietnamToday.AddYears(1)), 7, RoleNames.Manager);

        var (contract, notification) = Assert.Single(repository.Added);
        Assert.Equal((5, 10, (int?)1, (int?)7), (contract.RoomId, contract.CustomerId, contract.BookingId, contract.ProcessedByStaffId));
        Assert.Equal((VietnamToday, VietnamToday.AddYears(1)), (contract.StartDate, contract.EndDate));
        Assert.Equal((3_500_000m, 3_000_000m, ContractStatuses.Active), (contract.MonthlyRent, contract.DepositAmount, contract.Status));
        Assert.Equal((10, NotificationTypes.Contract, false), (notification.UserId, notification.Type, notification.IsRead));
        Assert.Contains("A-101", notification.Title);
        Assert.Equal(1, created.ContractId);
    }

    [Fact]
    public async Task CreateFromBookingAsync_does_not_allow_an_owner_admin_to_perform_manager_operations()
    {
        var repository = new FakeContractRepository();

        await Assert.ThrowsAsync<ContractAuthorizationException>(() => CreateService(repository)
            .CreateFromBookingAsync(
                new CreateContractDto(1, VietnamToday, VietnamToday.AddYears(1)),
                7,
                RoleNames.Admin));

        Assert.Empty(repository.Added);
    }

    [Fact]
    public async Task CreateFromBookingAsync_uses_the_vietnam_calendar_day_for_the_start_date()
    {
        var repository = new FakeContractRepository();

        await Assert.ThrowsAsync<ContractBusinessRuleException>(() => CreateService(repository)
            .CreateFromBookingAsync(new CreateContractDto(1, VietnamToday.AddDays(-1), VietnamToday.AddMonths(6)), 7, RoleNames.Manager));
        Assert.Empty(repository.Added);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreateFromBookingAsync_requires_end_date_after_start_date(int daysAfterStart)
    {
        await Assert.ThrowsAsync<ContractBusinessRuleException>(() => CreateService(new FakeContractRepository())
            .CreateFromBookingAsync(new CreateContractDto(1, VietnamToday, VietnamToday.AddDays(daysAfterStart)), 7, RoleNames.Manager));
    }

    [Theory]
    [InlineData(RoleNames.Customer, 10)]
    [InlineData(RoleNames.Manager, 8)]
    [InlineData(RoleNames.Admin, 99)]
    public async Task CreateFromBookingAsync_requires_an_assigned_manager(string role, int userId)
    {
        var repository = new FakeContractRepository();

        await Assert.ThrowsAsync<ContractAuthorizationException>(() => CreateService(repository)
            .CreateFromBookingAsync(new CreateContractDto(1, VietnamToday, VietnamToday.AddYears(1)), userId, role));
        Assert.Empty(repository.Added);
    }

    [Fact]
    public async Task CreateFromBookingAsync_rejects_missing_booking()
    {
        await Assert.ThrowsAsync<ContractNotFoundException>(() => CreateService(new FakeContractRepository { Booking = null })
            .CreateFromBookingAsync(new CreateContractDto(1, VietnamToday, VietnamToday.AddYears(1)), 7, RoleNames.Manager));
    }

    [Fact]
    public async Task CreateFromBookingAsync_requires_a_successful_booking_payment()
    {
        var repository = new FakeContractRepository
        {
            Booking = SampleBooking() with { HasSuccessfulPayment = false }
        };

        await Assert.ThrowsAsync<ContractConflictException>(() => CreateService(repository)
            .CreateFromBookingAsync(
                new CreateContractDto(1, VietnamToday, VietnamToday.AddYears(1)),
                7,
                RoleNames.Manager));

        Assert.Empty(repository.Added);
    }

    [Theory]
    [InlineData(BookingStatuses.Pending, false)]
    [InlineData(BookingStatuses.Cancelled, false)]
    [InlineData(BookingStatuses.Deposited, true)]
    public async Task CreateFromBookingAsync_requires_a_deposited_booking_without_contract(string status, bool hasContract)
    {
        var repository = new FakeContractRepository { Booking = SampleBooking() with { Status = status, HasContract = hasContract } };

        await Assert.ThrowsAsync<ContractConflictException>(() => CreateService(repository)
            .CreateFromBookingAsync(new CreateContractDto(1, VietnamToday, VietnamToday.AddYears(1)), 7, RoleNames.Manager));
        Assert.Empty(repository.Added);
    }

    [Fact]
    public async Task CreateFromBookingAsync_reports_conflict_when_room_or_booking_changed_before_saving()
    {
        var repository = new FakeContractRepository { AddSucceeds = false };

        await Assert.ThrowsAsync<ContractConflictException>(() => CreateService(repository)
            .CreateFromBookingAsync(new CreateContractDto(1, VietnamToday, VietnamToday.AddYears(1)), 7, RoleNames.Manager));
    }

    [Theory]
    [InlineData(RoleNames.Customer, 42, null)]
    [InlineData(RoleNames.Manager, null, 42)]
    [InlineData(RoleNames.Admin, null, null)]
    public async Task GetAllAsync_scopes_list_by_role(string role, int? expectedCustomerId, int? expectedOwnerId)
    {
        var repository = new FakeContractRepository();

        await CreateService(repository).GetAllAsync(42, role);

        Assert.Equal((expectedCustomerId, expectedOwnerId), repository.LastScope);
    }

    [Fact]
    public async Task GetByIdAsync_denies_existing_contract_outside_scope()
    {
        var repository = new FakeContractRepository { ExistingContractId = 1 };

        await Assert.ThrowsAsync<ContractAuthorizationException>(() =>
            CreateService(repository).GetByIdAsync(1, 20, RoleNames.Customer));
    }

    [Fact]
    public async Task GetByIdAsync_displays_terminated_contracts_as_terminated()
    {
        var repository = new FakeContractRepository
        {
            ExistingContractId = 1,
            ExistingContractStatus = "Terminated"
        };

        var contract = await CreateService(repository).GetByIdAsync(1, 10, RoleNames.Customer);

        Assert.Equal(ContractStatuses.Terminated, contract!.DisplayStatus);
    }

    [Fact]
    public async Task GetByIdAsync_displays_an_expired_active_contract_as_pending_checkout()
    {
        var repository = new FakeContractRepository
        {
            ExistingContractId = 1,
            ExistingEndDate = VietnamToday.AddDays(-1)
        };

        var contract = await CreateService(repository).GetByIdAsync(1, 10, RoleNames.Customer);

        Assert.Equal("ExpiredPendingCheckout", contract!.DisplayStatus);
        Assert.Equal(ContractStatuses.Active, contract.Status);
    }

    [Fact]
    public async Task ExtendContractAsync_updates_the_existing_contract_and_keeps_its_business_terms()
    {
        var repository = new FakeContractRepository { ExistingContractId = 1 };
        var newEndDate = VietnamToday.AddYears(2);

        var updated = await CreateService(repository).ExtendContractAsync(
            1,
            new ExtendContractRequestDto(newEndDate),
            7,
            RoleNames.Manager,
            CancellationToken.None);

        Assert.Equal((1, newEndDate, ContractStatuses.Active),
            (updated.ContractId, updated.EndDate, updated.Status));
        Assert.Equal(3_500_000m, updated.MonthlyRent);
        Assert.Empty(repository.Added);
        Assert.Equal(1, repository.ExtensionCount);
        Assert.Equal((10, NotificationTypes.Contract),
            (Assert.Single(repository.MutationNotifications).UserId, repository.MutationNotifications[0].Type));
    }

    [Theory]
    [InlineData(RoleNames.Customer, 10)]
    [InlineData(RoleNames.Admin, 9)]
    [InlineData(RoleNames.Manager, 8)]
    public async Task ExtendContractAsync_requires_an_assigned_manager(string role, int userId)
    {
        var repository = new FakeContractRepository { ExistingContractId = 1 };

        await Assert.ThrowsAsync<ContractAuthorizationException>(() => CreateService(repository)
            .ExtendContractAsync(
                1,
                new ExtendContractRequestDto(VietnamToday.AddYears(2)),
                userId,
                role,
                CancellationToken.None));

        Assert.Equal(0, repository.ExtensionCount);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public async Task ExtendContractAsync_requires_a_future_end_date_after_the_current_end_date(int daysAfterEnd)
    {
        var repository = new FakeContractRepository { ExistingContractId = 1 };

        await Assert.ThrowsAsync<ContractBusinessRuleException>(() => CreateService(repository)
            .ExtendContractAsync(
                1,
                new ExtendContractRequestDto(VietnamToday.AddYears(1).AddDays(daysAfterEnd)),
                7,
                RoleNames.Manager,
                CancellationToken.None));

        Assert.Equal(0, repository.ExtensionCount);
    }

    [Fact]
    public async Task ExtendContractAsync_allows_a_late_extension_while_the_contract_still_holds_the_room()
    {
        var repository = new FakeContractRepository
        {
            ExistingContractId = 1,
            ExistingEndDate = VietnamToday.AddDays(-1)
        };

        var updated = await CreateService(repository).ExtendContractAsync(
            1,
            new ExtendContractRequestDto(VietnamToday.AddMonths(6)),
            7,
            RoleNames.Manager,
            CancellationToken.None);

        Assert.Equal(VietnamToday.AddMonths(6), updated.EndDate);
    }

    [Fact]
    public async Task ExtendContractAsync_rejects_a_terminated_contract()
    {
        var repository = new FakeContractRepository
        {
            ExistingContractId = 1,
            ExistingContractStatus = ContractStatuses.Terminated
        };

        await Assert.ThrowsAsync<ContractConflictException>(() => CreateService(repository)
            .ExtendContractAsync(
                1,
                new ExtendContractRequestDto(VietnamToday.AddYears(2)),
                7,
                RoleNames.Manager,
                CancellationToken.None));

        Assert.Equal(0, repository.ExtensionCount);
    }

    [Fact]
    public async Task TerminateContractAsync_ends_the_contract_and_returns_recalculated_debt()
    {
        var repository = new FakeContractRepository
        {
            ExistingContractId = 1,
            OutstandingDebt = 3_237_500m
        };

        var result = await CreateService(repository).TerminateContractAsync(
            1,
            new TerminateContractRequestDto(true, true, true),
            7,
            RoleNames.Manager,
            CancellationToken.None);

        Assert.Equal(ContractStatuses.Terminated, result.Contract.Status);
        Assert.Equal(3_237_500m, result.OutstandingDebt);
        Assert.Equal(1, repository.TerminationCount);
        Assert.Equal((true, true), (repository.LastRoomReadyForRental, repository.LastDebtAcknowledged));
    }

    [Fact]
    public async Task TerminateContractAsync_requires_checkout_confirmation_and_debt_acknowledgement()
    {
        var notHandedOver = new FakeContractRepository { ExistingContractId = 1 };
        await Assert.ThrowsAsync<ContractBusinessRuleException>(() => CreateService(notHandedOver)
            .TerminateContractAsync(
                1,
                new TerminateContractRequestDto(false, true, true),
                7,
                RoleNames.Manager,
                CancellationToken.None));
        Assert.Equal(0, notHandedOver.TerminationCount);

        var outstandingDebt = new FakeContractRepository { ExistingContractId = 1, OutstandingDebt = 1_000m };
        await Assert.ThrowsAsync<ContractBusinessRuleException>(() => CreateService(outstandingDebt)
            .TerminateContractAsync(
                1,
                new TerminateContractRequestDto(true, false, false),
                7,
                RoleNames.Manager,
                CancellationToken.None));
        Assert.Equal(0, outstandingDebt.TerminationCount);
    }

    [Theory]
    [InlineData(RoleNames.Customer, 10)]
    [InlineData(RoleNames.Admin, 9)]
    [InlineData(RoleNames.Manager, 8)]
    public async Task TerminateContractAsync_requires_an_assigned_manager(string role, int userId)
    {
        var repository = new FakeContractRepository { ExistingContractId = 1 };

        await Assert.ThrowsAsync<ContractAuthorizationException>(() => CreateService(repository)
            .TerminateContractAsync(
                1,
                new TerminateContractRequestDto(true, true, true),
                userId,
                role,
                CancellationToken.None));

        Assert.Equal(0, repository.TerminationCount);
    }

    [Fact]
    public async Task MarkTerminatedRoomReadyAsync_requires_a_terminated_contract()
    {
        var repository = new FakeContractRepository
        {
            ExistingContractId = 1,
            ExistingContractStatus = ContractStatuses.Terminated
        };

        await CreateService(repository).MarkTerminatedRoomReadyAsync(
            1,
            7,
            RoleNames.Manager,
            CancellationToken.None);

        Assert.Equal(1, repository.MarkRoomReadyCount);

        repository.ExistingContractStatus = ContractStatuses.Active;
        await Assert.ThrowsAsync<ContractConflictException>(() => CreateService(repository)
            .MarkTerminatedRoomReadyAsync(1, 7, RoleNames.Manager, CancellationToken.None));
    }

    private static ContractService CreateService(FakeContractRepository repository) =>
        new(repository, new FixedTimeProvider(Now));

    private static ContractBookingReadModel SampleBooking() =>
        new(1, 5, "A-101", 10, 7, BookingStatuses.Deposited, 3_000_000m, 3_500_000m, false,
            true, "Property owner", "Property address", 1);

    private sealed class FakeContractRepository : IContractRepository
    {
        public ContractBookingReadModel? Booking { get; init; } = SampleBooking();
        public bool AddSucceeds { get; init; } = true;
        public int? ExistingContractId
        {
            get; set;
        }
        public string ExistingContractStatus { get; set; } = ContractStatuses.Active;
        public DateOnly ExistingEndDate { get; set; } = VietnamToday.AddYears(1);
        public decimal OutstandingDebt { get; init; }
        public int ExtensionCount { get; private set; }
        public int TerminationCount { get; private set; }
        public int MarkRoomReadyCount { get; private set; }
        public bool LastRoomReadyForRental { get; private set; }
        public bool LastDebtAcknowledged { get; private set; }
        public List<Notifications> MutationNotifications { get; } = [];
        public List<(Contracts Contract, Notifications Notification)> Added { get; } = [];
        public (int? CustomerId, int? OwnerId) LastScope
        {
            get; private set;
        }

        public Task<ContractBookingReadModel?> GetBookingAsync(int bookingId) =>
            Task.FromResult(Booking?.BookingId == bookingId ? Booking : null);

        public Task<bool> HasActiveManagerAssignmentAsync(int propertyId, int managerId) =>
            Task.FromResult(propertyId == 1 && managerId == 7);

        public Task<bool> AddFromBookingAsync(Contracts contract, Notifications customerNotification)
        {
            if (!AddSucceeds)
            {
                return Task.FromResult(false);
            }

            contract.ContractId = 1;
            ExistingContractId = 1;
            Added.Add((contract, customerNotification));
            return Task.FromResult(true);
        }

        public Task<List<ContractReadModel>> GetAllAsync(int? customerId, int? ownerId)
        {
            LastScope = (customerId, ownerId);
            return Task.FromResult(new List<ContractReadModel>());
        }

        public Task<List<InvoiceReadModel>> GetIssuedInvoicesForTimelineAsync(
            int contractId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new List<InvoiceReadModel>());
        }

        public Task<ContractMutationResult> ExtendAsync(
            int contractId,
            int managerId,
            DateOnly newEndDate,
            DateOnly today,
            DateTimeOffset asOfUtc,
            Notifications customerNotification,
            CancellationToken cancellationToken)
        {
            ExtensionCount++;
            ExistingEndDate = newEndDate;
            MutationNotifications.Add(customerNotification);
            return Task.FromResult(new ContractMutationResult(
                ContractMutationStatus.Updated,
                CreateContractReadModel(contractId)));
        }

        public Task<ContractMutationResult> TerminateAsync(
            int contractId,
            int managerId,
            bool roomReadyForRental,
            bool outstandingDebtAcknowledged,
            DateTimeOffset asOfUtc,
            Notifications customerNotification,
            CancellationToken cancellationToken)
        {
            if (OutstandingDebt > 0m && !outstandingDebtAcknowledged)
            {
                return Task.FromResult(new ContractMutationResult(
                    ContractMutationStatus.Conflict,
                    OutstandingDebt: OutstandingDebt));
            }

            TerminationCount++;
            LastRoomReadyForRental = roomReadyForRental;
            LastDebtAcknowledged = outstandingDebtAcknowledged;
            ExistingContractStatus = ContractStatuses.Terminated;
            MutationNotifications.Add(customerNotification);
            return Task.FromResult(new ContractMutationResult(
                ContractMutationStatus.Updated,
                CreateContractReadModel(contractId),
                OutstandingDebt));
        }

        public Task<ContractMutationStatus> MarkTerminatedRoomReadyAsync(
            int contractId,
            int managerId,
            DateTimeOffset asOfUtc,
            CancellationToken cancellationToken)
        {
            MarkRoomReadyCount++;
            return Task.FromResult(ContractMutationStatus.Updated);
        }

        // Hợp đồng mẫu thuộc khách 10 ở khu trọ của chủ 7; ngoài phạm vi đó thì trả null như repository thật.
        public Task<ContractReadModel?> GetByIdAsync(int contractId, int? customerId, int? ownerId)
        {
            var visible = ExistingContractId == contractId && (customerId is null or 10) && (ownerId is null or 7);
            return Task.FromResult(visible
                ? CreateContractReadModel(contractId)
                : null);
        }

        private ContractReadModel CreateContractReadModel(int contractId) =>
            new(contractId, 1, 5, "A-101", 1, "Sunrise", 10, "Customer", "0900000000",
                VietnamToday, ExistingEndDate, 3_500_000m, 3_000_000m, ExistingContractStatus, "Manager");

        public Task<ContractMutationResult> SaveMeterHandoverAsync(
            int contractId, int managerId, DateOnly handoverDate, decimal electric, decimal water,
            DateOnly? expectedDate, decimal? expectedElectric, decimal? expectedWater, DateOnly today,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> ExistsAsync(int contractId) => Task.FromResult(ExistingContractId == contractId);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
