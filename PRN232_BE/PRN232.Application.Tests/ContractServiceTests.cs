using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
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
    public async Task CreateFromBookingAsync_uses_the_vietnam_calendar_day_for_the_start_date()
    {
        var repository = new FakeContractRepository();

        await Assert.ThrowsAsync<ContractBusinessRuleException>(() => CreateService(repository)
            .CreateFromBookingAsync(new CreateContractDto(1, VietnamToday.AddDays(-1), VietnamToday.AddMonths(6)), 9, RoleNames.Admin));
        Assert.Empty(repository.Added);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreateFromBookingAsync_requires_end_date_after_start_date(int daysAfterStart)
    {
        await Assert.ThrowsAsync<ContractBusinessRuleException>(() => CreateService(new FakeContractRepository())
            .CreateFromBookingAsync(new CreateContractDto(1, VietnamToday, VietnamToday.AddDays(daysAfterStart)), 9, RoleNames.Admin));
    }

    [Theory]
    [InlineData(RoleNames.Customer, 10)]
    [InlineData(RoleNames.Manager, 8)]
    public async Task CreateFromBookingAsync_allows_only_the_owner_or_an_admin(string role, int userId)
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
            .CreateFromBookingAsync(new CreateContractDto(1, VietnamToday, VietnamToday.AddYears(1)), 9, RoleNames.Admin));
    }

    [Theory]
    [InlineData(BookingStatuses.Pending, false)]
    [InlineData(BookingStatuses.Cancelled, false)]
    [InlineData(BookingStatuses.Deposited, true)]
    public async Task CreateFromBookingAsync_requires_a_deposited_booking_without_contract(string status, bool hasContract)
    {
        var repository = new FakeContractRepository { Booking = SampleBooking() with { Status = status, HasContract = hasContract } };

        await Assert.ThrowsAsync<ContractConflictException>(() => CreateService(repository)
            .CreateFromBookingAsync(new CreateContractDto(1, VietnamToday, VietnamToday.AddYears(1)), 9, RoleNames.Admin));
        Assert.Empty(repository.Added);
    }

    [Fact]
    public async Task CreateFromBookingAsync_reports_conflict_when_room_or_booking_changed_before_saving()
    {
        var repository = new FakeContractRepository { AddSucceeds = false };

        await Assert.ThrowsAsync<ContractConflictException>(() => CreateService(repository)
            .CreateFromBookingAsync(new CreateContractDto(1, VietnamToday, VietnamToday.AddYears(1)), 9, RoleNames.Admin));
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

    private static ContractService CreateService(FakeContractRepository repository) =>
        new(repository, new FixedTimeProvider(Now));

    private static ContractBookingDto SampleBooking() =>
        new(1, 5, "A-101", 10, 7, BookingStatuses.Deposited, 3_000_000m, 3_500_000m, false);

    private sealed class FakeContractRepository : IContractRepository
    {
        public ContractBookingDto? Booking { get; init; } = SampleBooking();
        public bool AddSucceeds { get; init; } = true;
        public int? ExistingContractId { get; set; }
        public List<(Contracts Contract, Notifications Notification)> Added { get; } = [];
        public (int? CustomerId, int? OwnerId) LastScope { get; private set; }

        public Task<ContractBookingDto?> GetBookingAsync(int bookingId) =>
            Task.FromResult(Booking?.BookingId == bookingId ? Booking : null);

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

        public Task<List<ContractDto>> GetAllAsync(int? customerId, int? ownerId)
        {
            LastScope = (customerId, ownerId);
            return Task.FromResult(new List<ContractDto>());
        }

        // Hợp đồng mẫu thuộc khách 10 ở khu trọ của chủ 7; ngoài phạm vi đó thì trả null như repository thật.
        public Task<ContractDto?> GetByIdAsync(int contractId, int? customerId, int? ownerId)
        {
            var visible = ExistingContractId == contractId && (customerId is null or 10) && (ownerId is null or 7);
            return Task.FromResult(visible
                ? new ContractDto(contractId, 1, 5, "A-101", 1, "Sunrise", 10, "Customer", "0900000000",
                    VietnamToday, VietnamToday.AddYears(1), 3_500_000m, 3_000_000m, ContractStatuses.Active, "Manager")
                : null);
        }

        public Task<bool> ExistsAsync(int contractId) => Task.FromResult(ExistingContractId == contractId);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
