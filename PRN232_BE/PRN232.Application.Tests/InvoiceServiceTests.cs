using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using Xunit;

namespace PRN232.Application.Tests;

public class InvoiceServiceTests
{
    // 18:00 UTC ngày 28/10 = 01:00 ngày 29/10 giờ Việt Nam.
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly October = new(2026, 10, 1);
    private static readonly DateOnly DueDate = new(2026, 11, 10);

    private static readonly BillableServiceDto Electricity = new(1, "Electricity - per kWh", 11, 3_500m, "kwh", "usage");
    private static readonly BillableServiceDto Water = new(2, "Water - per m3", 12, 20_000m, "m3", "usage");
    private static readonly BillableServiceDto Internet = new(3, "Internet - monthly", 13, 100_000m, "room_month", "fixed_room");

    [Fact]
    public async Task PreviewAsync_bills_rent_metered_usage_and_fixed_services_without_saving()
    {
        var repository = new FakeInvoiceRepository();

        var preview = await CreateService(repository).PreviewAsync(new CreateInvoiceDto(1, October, DueDate), 7, RoleNames.Manager);

        Assert.Equal(("Preview", 0), (preview.Status, preview.InvoiceId));
        Assert.Equal(
            new[] { (InvoiceItemTypes.Rent, 2_500_000m), (InvoiceItemTypes.Electricity, 507_500m), (InvoiceItemTypes.Water, 100_000m), (InvoiceItemTypes.Service, 100_000m) },
            preview.Lines.Select(line => (line.ItemType, line.Amount)));
        Assert.Equal(3_207_500m, preview.Total);
        Assert.Empty(repository.Added);
    }

    [Fact]
    public async Task CreateAsync_saves_a_price_snapshot_and_notifies_the_tenant()
    {
        var repository = new FakeInvoiceRepository();

        var created = await CreateService(repository).CreateAsync(new CreateInvoiceDto(1, October, DueDate), 9, RoleNames.Admin);

        var (invoice, notification) = Assert.Single(repository.Added);
        Assert.Equal((1, October, DueDate, InvoiceStatuses.Unpaid), (invoice.ContractId, invoice.InvoiceMonth, invoice.DueDate, invoice.Status));
        var electricity = invoice.InvoiceDetails.Single(detail => detail.ItemType == InvoiceItemTypes.Electricity);
        Assert.Equal(((int?)1, (int?)11, (decimal?)145m, (decimal?)3_500m, "kwh", "usage"),
            (electricity.ServiceId, electricity.RateId, electricity.Quantity, electricity.UnitPrice, electricity.UnitCode, electricity.BillingBasis));
        Assert.Equal((10, NotificationTypes.Invoice), (notification.UserId, notification.Type));
        Assert.Contains("10/2026", notification.Title);
        Assert.Contains("3.207.500", notification.Title);
        Assert.Equal(3_207_500m, created.Total);
    }

    [Fact]
    public async Task Amounts_are_rounded_to_whole_dong()
    {
        var repository = new FakeInvoiceRepository
        {
            Services = [Electricity with { Price = 3_333m }],
            Usage = new MonthUsageDto(1.5m, 0m)
        };

        var preview = await CreateService(repository).PreviewAsync(new CreateInvoiceDto(1, October, DueDate), 7, RoleNames.Manager);

        Assert.Equal(5_000m, preview.Lines.Single(line => line.ItemType == InvoiceItemTypes.Electricity).Amount);
    }

    [Fact]
    public async Task Metered_services_need_this_months_reading_but_fixed_services_do_not()
    {
        var withMeter = new FakeInvoiceRepository { Usage = null };
        var fixedOnly = new FakeInvoiceRepository { Usage = null, Services = [Internet] };

        await Assert.ThrowsAsync<InvoiceConflictException>(() =>
            CreateService(withMeter).PreviewAsync(new CreateInvoiceDto(1, October, DueDate), 7, RoleNames.Manager));
        var preview = await CreateService(fixedOnly).PreviewAsync(new CreateInvoiceDto(1, October, DueDate), 7, RoleNames.Manager);

        Assert.Equal(2_600_000m, preview.Total);
    }

    [Fact]
    public async Task A_service_without_a_price_or_with_an_unsupported_basis_is_never_silently_skipped()
    {
        var noPrice = new FakeInvoiceRepository { Services = [Internet with { RateId = null, Price = null }] };
        var perPerson = new FakeInvoiceRepository { Services = [Internet with { UnitCode = "person_month", BillingBasis = "occupant" }] };

        await Assert.ThrowsAsync<InvoiceConflictException>(() =>
            CreateService(noPrice).PreviewAsync(new CreateInvoiceDto(1, October, DueDate), 7, RoleNames.Manager));
        await Assert.ThrowsAsync<InvoiceBusinessRuleException>(() =>
            CreateService(perPerson).PreviewAsync(new CreateInvoiceDto(1, October, DueDate), 7, RoleNames.Manager));
    }

    [Theory]
    [InlineData(2026, 10, 15, 2026, 11, 10)]
    [InlineData(2026, 11, 1, 2026, 11, 10)]
    [InlineData(2026, 5, 1, 2026, 11, 10)]
    [InlineData(2026, 10, 1, 2026, 10, 28)]
    public async Task Rejects_mid_month_future_out_of_term_months_and_past_due_dates(
        int year, int month, int day, int dueYear, int dueMonth, int dueDay)
    {
        var repository = new FakeInvoiceRepository();

        await Assert.ThrowsAsync<InvoiceBusinessRuleException>(() => CreateService(repository).CreateAsync(
            new CreateInvoiceDto(1, new DateOnly(year, month, day), new DateOnly(dueYear, dueMonth, dueDay)), 7, RoleNames.Manager));
        Assert.Empty(repository.Added);
    }

    [Fact]
    public async Task Only_one_invoice_per_contract_and_month()
    {
        await Assert.ThrowsAsync<InvoiceConflictException>(() => CreateService(new FakeInvoiceRepository { InvoiceExists = true })
            .PreviewAsync(new CreateInvoiceDto(1, October, DueDate), 7, RoleNames.Manager));
        await Assert.ThrowsAsync<InvoiceConflictException>(() => CreateService(new FakeInvoiceRepository { AddSucceeds = false })
            .CreateAsync(new CreateInvoiceDto(1, October, DueDate), 7, RoleNames.Manager));
    }

    [Theory]
    [InlineData(RoleNames.Customer, 10)]
    [InlineData(RoleNames.Manager, 8)]
    public async Task Only_the_owner_or_an_admin_can_bill_a_contract(string role, int userId)
    {
        await Assert.ThrowsAsync<InvoiceAuthorizationException>(() => CreateService(new FakeInvoiceRepository())
            .PreviewAsync(new CreateInvoiceDto(1, October, DueDate), userId, role));
    }

    [Fact]
    public async Task Rejects_missing_or_inactive_contracts()
    {
        await Assert.ThrowsAsync<InvoiceNotFoundException>(() => CreateService(new FakeInvoiceRepository { Contract = null })
            .PreviewAsync(new CreateInvoiceDto(1, October, DueDate), 9, RoleNames.Admin));
        await Assert.ThrowsAsync<InvoiceConflictException>(() =>
            CreateService(new FakeInvoiceRepository { Contract = SampleContract() with { Status = "Terminated" } })
                .PreviewAsync(new CreateInvoiceDto(1, October, DueDate), 9, RoleNames.Admin));
    }

    [Theory]
    [InlineData(RoleNames.Customer, null, 42, null)]
    [InlineData(RoleNames.Manager, InvoiceStatuses.Unpaid, null, 42)]
    [InlineData(RoleNames.Admin, InvoiceStatuses.Paid, null, null)]
    public async Task GetAllAsync_scopes_by_role_and_passes_filters(string role, string? status, int? expectedCustomerId, int? expectedOwnerId)
    {
        var repository = new FakeInvoiceRepository();

        await CreateService(repository).GetAllAsync(42, role, 1, status);

        Assert.Equal((expectedCustomerId, expectedOwnerId, (int?)1, status), repository.LastQuery);
    }

    [Fact]
    public async Task GetAllAsync_rejects_unknown_status()
    {
        await Assert.ThrowsAsync<InvoiceBusinessRuleException>(() =>
            CreateService(new FakeInvoiceRepository()).GetAllAsync(42, RoleNames.Admin, null, "Draft"));
    }

    private static InvoiceService CreateService(FakeInvoiceRepository repository) => new(repository, new FixedTimeProvider(Now));

    private static InvoiceContractDto SampleContract() => new(
        1, 7, 10, "Customer A", "A-101", 1, "Sunrise", 1, 2_500_000m, ContractStatuses.Active,
        new DateOnly(2026, 6, 1), new DateOnly(2027, 5, 31));

    private sealed class FakeInvoiceRepository : IInvoiceRepository
    {
        public InvoiceContractDto? Contract { get; init; } = SampleContract();
        public List<BillableServiceDto> Services { get; init; } = [Electricity, Water, Internet];
        public MonthUsageDto? Usage { get; init; } = new(145m, 5m);
        public bool InvoiceExists { get; init; }
        public bool AddSucceeds { get; init; } = true;
        public List<(Invoices Invoice, Notifications Notification)> Added { get; } = [];
        public (int? CustomerId, int? OwnerId, int? ContractId, string? Status) LastQuery { get; private set; }

        public Task<InvoiceContractDto?> GetContractAsync(int contractId) =>
            Task.FromResult(Contract?.ContractId == contractId ? Contract : null);

        public Task<List<BillableServiceDto>> GetBillableServicesAsync(int propertyId, int roomTypeId, DateOnly month) =>
            Task.FromResult(Services);

        public Task<MonthUsageDto?> GetUsageAsync(int contractId, DateOnly month) => Task.FromResult(Usage);

        public Task<bool> ExistsForMonthAsync(int contractId, DateOnly month) => Task.FromResult(InvoiceExists);

        public Task<bool> AddIfAbsentAsync(Invoices invoice, Notifications customerNotification)
        {
            if (!AddSucceeds)
            {
                return Task.FromResult(false);
            }

            invoice.InvoiceId = Added.Count + 1;
            Added.Add((invoice, customerNotification));
            return Task.FromResult(true);
        }

        public Task<List<InvoiceDto>> GetAllAsync(int? customerId, int? ownerId, int? contractId, string? status)
        {
            LastQuery = (customerId, ownerId, contractId, status);
            return Task.FromResult(new List<InvoiceDto>());
        }

        public Task<InvoiceDto?> GetByIdAsync(int invoiceId, int? customerId, int? ownerId)
        {
            var invoice = Added.Select(item => item.Invoice).SingleOrDefault(item => item.InvoiceId == invoiceId);
            return Task.FromResult(invoice is null ? null : new InvoiceDto(
                invoice.InvoiceId, invoice.ContractId, "A-101", "Sunrise", "Customer A", invoice.InvoiceMonth, invoice.DueDate,
                invoice.Status, invoice.InvoiceDetails.Sum(detail => detail.Amount),
                invoice.InvoiceDetails.Select(detail => new InvoiceLineDto(
                    detail.ItemType, detail.Description, detail.Quantity, detail.UnitPrice, detail.UnitCode, detail.Amount)).ToList()));
        }

        public Task<bool> ExistsAsync(int invoiceId) => Task.FromResult(Added.Any(item => item.Invoice.InvoiceId == invoiceId));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
