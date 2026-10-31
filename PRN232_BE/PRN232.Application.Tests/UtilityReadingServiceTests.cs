using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.Services;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using Xunit;

namespace PRN232.Application.Tests;

public class UtilityReadingServiceTests
{
    // 18:00 UTC ngày 28/10 = 01:00 ngày 29/10 giờ Việt Nam, tháng hiện tại là 10/2026.
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly October = new(2026, 10, 1);

    [Fact]
    public async Task CreateAsync_takes_the_starting_values_from_the_first_reading_request()
    {
        var repository = new FakeReadingRepository();

        var created = await CreateService(repository).CreateAsync(
            new CreateUtilityReadingDto(1, October, 150m, 14m, ElectricStart: 100m, WaterStart: 10m), 7, RoleNames.Manager);

        var added = Assert.Single(repository.Added);
        Assert.Equal((1, October), (added.ContractId, added.ReadingMonth));
        Assert.Equal((100m, 150m, 10m, 14m), (added.ElectricStart, added.ElectricEnd, added.WaterStart, added.WaterEnd));
        Assert.Equal(1, created.ReadingId);
    }

    [Fact]
    public async Task CreateAsync_continues_from_the_previous_month_and_ignores_client_starting_values()
    {
        var repository = new FakeReadingRepository { Latest = new LatestReadingReadModel(new DateOnly(2026, 9, 1), 1320m, 104m) };

        await CreateService(repository).CreateAsync(
            new CreateUtilityReadingDto(1, October, 1465m, 109m, ElectricStart: 0m, WaterStart: 0m), 9, RoleNames.Admin);

        var added = Assert.Single(repository.Added);
        Assert.Equal((1320m, 104m), (added.ElectricStart, added.WaterStart));
    }

    [Fact]
    public async Task CreateAsync_requires_starting_values_for_the_first_reading()
    {
        var repository = new FakeReadingRepository();

        await Assert.ThrowsAsync<UtilityReadingBusinessRuleException>(() => CreateService(repository)
            .CreateAsync(new CreateUtilityReadingDto(1, October, 150m, 14m, null, null), 7, RoleNames.Manager));
        Assert.Empty(repository.Added);
    }

    [Theory]
    [InlineData(2026, 10, 15)]
    [InlineData(2026, 11, 1)]
    [InlineData(2026, 5, 1)]
    public async Task CreateAsync_rejects_mid_month_future_or_out_of_term_months(int year, int month, int day)
    {
        var repository = new FakeReadingRepository { Latest = new LatestReadingReadModel(new DateOnly(2026, 4, 1), 100m, 10m) };

        await Assert.ThrowsAsync<UtilityReadingBusinessRuleException>(() => CreateService(repository)
            .CreateAsync(new CreateUtilityReadingDto(1, new DateOnly(year, month, day), 150m, 14m, null, null), 7, RoleNames.Manager));
        Assert.Empty(repository.Added);
    }

    [Fact]
    public async Task CreateAsync_uses_the_vietnam_calendar_month()
    {
        var repository = new FakeReadingRepository { Latest = new LatestReadingReadModel(October, 150m, 14m) };
        // 18:00 UTC ngày 31/10 đã là ngày 01/11 ở Việt Nam, nên được ghi chỉ số tháng 11.
        var service = new UtilityReadingService(repository, new FixedTimeProvider(new DateTimeOffset(2026, 10, 31, 18, 0, 0, TimeSpan.Zero)));

        await service.CreateAsync(new CreateUtilityReadingDto(1, new DateOnly(2026, 11, 1), 200m, 18m, null, null), 7, RoleNames.Manager);

        Assert.Single(repository.Added);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(9)]
    public async Task CreateAsync_rejects_a_month_that_is_not_after_the_latest_reading(int month)
    {
        var repository = new FakeReadingRepository { Latest = new LatestReadingReadModel(October, 150m, 14m) };

        await Assert.ThrowsAsync<UtilityReadingConflictException>(() => CreateService(repository)
            .CreateAsync(new CreateUtilityReadingDto(1, new DateOnly(2026, month, 1), 200m, 18m, null, null), 7, RoleNames.Manager));
    }

    [Theory]
    [InlineData(1319, 110)]
    [InlineData(1400, 103)]
    public async Task CreateAsync_rejects_meter_values_lower_than_the_previous_ones(int electricEnd, int waterEnd)
    {
        var repository = new FakeReadingRepository { Latest = new LatestReadingReadModel(new DateOnly(2026, 9, 1), 1320m, 104m) };

        await Assert.ThrowsAsync<UtilityReadingBusinessRuleException>(() => CreateService(repository)
            .CreateAsync(new CreateUtilityReadingDto(1, October, electricEnd, waterEnd, null, null), 7, RoleNames.Manager));
        Assert.Empty(repository.Added);
    }

    [Theory]
    [InlineData(RoleNames.Customer, 10)]
    [InlineData(RoleNames.Manager, 8)]
    public async Task CreateAsync_allows_only_the_owner_or_an_admin(string role, int userId)
    {
        await Assert.ThrowsAsync<UtilityReadingAuthorizationException>(() => CreateService(new FakeReadingRepository())
            .CreateAsync(new CreateUtilityReadingDto(1, October, 150m, 14m, 100m, 10m), userId, role));
    }

    [Fact]
    public async Task CreateAsync_rejects_missing_or_inactive_contract()
    {
        await Assert.ThrowsAsync<UtilityReadingNotFoundException>(() => CreateService(new FakeReadingRepository { Contract = null })
            .CreateAsync(new CreateUtilityReadingDto(1, October, 150m, 14m, 100m, 10m), 9, RoleNames.Admin));
        await Assert.ThrowsAsync<UtilityReadingConflictException>(() =>
            CreateService(new FakeReadingRepository { Contract = SampleContract() with { Status = "Terminated" } })
                .CreateAsync(new CreateUtilityReadingDto(1, October, 150m, 14m, 100m, 10m), 9, RoleNames.Admin));
    }

    [Fact]
    public async Task CreateAsync_reports_conflict_when_another_reading_was_saved_first()
    {
        var repository = new FakeReadingRepository { AddSucceeds = false };

        await Assert.ThrowsAsync<UtilityReadingConflictException>(() => CreateService(repository)
            .CreateAsync(new CreateUtilityReadingDto(1, October, 150m, 14m, 100m, 10m), 9, RoleNames.Admin));
    }

    [Theory]
    [InlineData(RoleNames.Customer, 42, null)]
    [InlineData(RoleNames.Manager, null, 42)]
    [InlineData(RoleNames.Admin, null, null)]
    public async Task GetAllAsync_scopes_by_role_and_filters_by_contract(string role, int? expectedCustomerId, int? expectedOwnerId)
    {
        var repository = new FakeReadingRepository();

        await CreateService(repository).GetAllAsync(42, role, 3);

        Assert.Equal((expectedCustomerId, expectedOwnerId, (int?)3), repository.LastQuery);
    }

    private static UtilityReadingService CreateService(FakeReadingRepository repository) =>
        new(repository, new FixedTimeProvider(Now));

    private static ReadingContractReadModel SampleContract() =>
        new(1, 7, ContractStatuses.Active, new DateOnly(2026, 6, 1), new DateOnly(2027, 5, 31));

    private sealed class FakeReadingRepository : IUtilityReadingRepository
    {
        public ReadingContractReadModel? Contract { get; init; } = SampleContract();
        public LatestReadingReadModel? Latest
        {
            get; init;
        }
        public bool AddSucceeds { get; init; } = true;
        public List<UtilityReadings> Added { get; } = [];
        public (int? CustomerId, int? OwnerId, int? ContractId) LastQuery
        {
            get; private set;
        }

        public Task<ReadingContractReadModel?> GetContractAsync(int contractId) =>
            Task.FromResult(Contract?.ContractId == contractId ? Contract : null);

        public Task<LatestReadingReadModel?> GetLatestAsync(int contractId) => Task.FromResult(Latest);

        public Task<bool> AddIfLatestAsync(UtilityReadings reading)
        {
            if (!AddSucceeds)
            {
                return Task.FromResult(false);
            }

            reading.ReadingId = Added.Count + 1;
            Added.Add(reading);
            return Task.FromResult(true);
        }

        public Task<List<UtilityReadingReadModel>> GetAllAsync(int? customerId, int? ownerId, int? contractId)
        {
            LastQuery = (customerId, ownerId, contractId);
            return Task.FromResult(new List<UtilityReadingReadModel>());
        }

        public Task<UtilityReadingReadModel?> GetByIdAsync(int readingId, int? customerId, int? ownerId)
        {
            var reading = Added.SingleOrDefault(item => item.ReadingId == readingId);
            return Task.FromResult(reading is null ? null : new UtilityReadingReadModel(
                reading.ReadingId, reading.ContractId, "A-101", "Sunrise", "Customer", reading.ReadingMonth,
                reading.ElectricStart, reading.ElectricEnd, reading.ElectricEnd - reading.ElectricStart,
                reading.WaterStart, reading.WaterEnd, reading.WaterEnd - reading.WaterStart));
        }

        public Task<bool> ExistsAsync(int readingId) => Task.FromResult(Added.Any(item => item.ReadingId == readingId));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
