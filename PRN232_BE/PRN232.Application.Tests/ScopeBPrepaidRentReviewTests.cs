using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using PRN232.Infrastructure.Repositories;
using Xunit;

namespace PRN232.Application.Tests;

public class ScopeBPrepaidRentReviewTests
{
    private static readonly DateOnly StartMonth = new(2026, 11, 1);
    private static readonly DateOnly Due = new(2026, 11, 30);

    [Theory]
    [InlineData(700, 700, 700, 200, 0)]
    [InlineData(1000, 700, 700, 200, 300)]
    [InlineData(350, 700, 350, 550, 0)]
    public async Task Paid_deposit_is_capped_to_rent_and_gateway_charges_only_net_service_balance(
        int deposit, int rent, int applied, int net, int unused)
    {
        await using var fixture = await CreditFixture.CreateAsync(deposit, rent);
        var request = new CreateInvoiceDto(1, StartMonth, Due);
        var preview = await fixture.Invoices.PreviewAsync(request, 7, RoleNames.Manager);
        Assert.Equal(0, await fixture.Context.Invoices.CountAsync());
        var invoice = await fixture.Invoices.CreateAsync(request, 7, RoleNames.Manager);
        Assert.Equal(preview.Lines.Select(row => row.Amount), invoice.Lines.Select(row => row.Amount));
        Assert.Equal(-applied, Assert.Single(invoice.Lines, row => row.ItemType == InvoiceItemTypes.PrepaidRentCredit).Amount);
        Assert.Equal((applied, net, net), (invoice.PrepaidRentApplied, invoice.NetPayable, invoice.RemainingPayable));

        var paid = await fixture.Invoices.PayAsync(invoice.InvoiceId, new PayInvoiceDto("MoMo"), 10);

        Assert.Equal("Paid", paid.Status);
        Assert.Equal(new decimal[] { net }, fixture.Gateway.Charges);
        var payment = await fixture.Context.Payments.SingleAsync(row => row.InvoiceId == invoice.InvoiceId);
        Assert.Equal(net, payment.Amount);
        var remaining = await new InvoiceRepository(fixture.Context).GetAvailablePrepaidRentCreditAsync(1);
        Assert.Equal(unused, remaining?.AvailableAmount ?? 0);
        Assert.Equal(1, await fixture.Context.InvoiceDetails.CountAsync(row => row.ItemType == InvoiceItemTypes.PrepaidRentCredit));
    }

    [Fact]
    public async Task Issuing_later_month_first_does_not_consume_credit_and_credit_cannot_be_reused()
    {
        await using var fixture = await CreditFixture.CreateAsync();
        await fixture.Context.Contracts.Where(row => row.ContractId == 1)
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.StartDate, StartMonth.AddMonths(-1)));
        var november = await fixture.Invoices.CreateAsync(new CreateInvoiceDto(1, StartMonth, Due), 7, RoleNames.Manager);
        Assert.DoesNotContain(november.Lines, row => row.ItemType == InvoiceItemTypes.PrepaidRentCredit);
        Assert.Equal(900m, november.Total);
        Assert.Equal(700m, (await new InvoiceRepository(fixture.Context).GetAvailablePrepaidRentCreditAsync(1))!.AvailableAmount);
        var octoberRequest = new CreateInvoiceDto(1, StartMonth.AddMonths(-1), Due);
        var october = await fixture.Invoices.CreateAsync(octoberRequest, 7, RoleNames.Manager);
        Assert.Equal(700m, october.PrepaidRentApplied);
        Assert.Equal(200m, october.Total);
        await Assert.ThrowsAsync<InvoiceConflictException>(() => fixture.Invoices.CreateAsync(octoberRequest, 7, RoleNames.Manager));
        await Assert.ThrowsAsync<InvoiceConflictException>(() => fixture.Invoices.CreateAsync(new CreateInvoiceDto(1, StartMonth, Due), 7, RoleNames.Manager));
        Assert.Equal(2, await fixture.Context.Invoices.CountAsync());
        Assert.Equal(1, await fixture.Context.InvoiceDetails.CountAsync(row => row.ItemType == InvoiceItemTypes.PrepaidRentCredit));
        Assert.Null(await new InvoiceRepository(fixture.Context).GetAvailablePrepaidRentCreditAsync(1));
        Assert.Equal(2, await fixture.Context.Notifications.CountAsync());
    }

    [Fact]
    public async Task Fully_credited_zero_invoice_is_paid_without_gateway_or_payment_and_repayment_is_rejected()
    {
        await using var fixture = await CreditFixture.CreateAsync(servicePrice: 0);
        var invoice = await fixture.Invoices.CreateAsync(new CreateInvoiceDto(1, StartMonth, Due), 7, RoleNames.Manager);
        Assert.Equal(("Paid", 0m, 0m, 700m), (invoice.Status, invoice.Total, invoice.RemainingPayable, invoice.PrepaidRentApplied));
        await Assert.ThrowsAsync<InvoiceConflictException>(() => fixture.Invoices.PayAsync(invoice.InvoiceId, new PayInvoiceDto("MoMo"), 10));
        await Assert.ThrowsAsync<InvoiceConflictException>(() => fixture.Invoices.RecordCashPaymentAsync(invoice.InvoiceId, 7, RoleNames.Manager));
        Assert.Empty(fixture.Gateway.Charges);
        Assert.Empty(await fixture.Context.Payments.Where(row => row.InvoiceId == invoice.InvoiceId).ToListAsync());
        Assert.Equal(1, await fixture.Context.Notifications.CountAsync());
    }

    [Fact]
    public async Task Termination_before_first_invoice_does_not_spend_credit_or_allow_a_new_invoice()
    {
        await using var fixture = await CreditFixture.CreateAsync();
        await fixture.Contracts.TerminateContractAsync(1, new TerminateContractRequestDto(true, true, false),
            7, RoleNames.Manager, CancellationToken.None);
        await Assert.ThrowsAsync<InvoiceConflictException>(() => fixture.Invoices.CreateAsync(
            new CreateInvoiceDto(1, StartMonth, Due), 7, RoleNames.Manager));
        Assert.Empty(await fixture.Context.Invoices.ToListAsync());
        Assert.Empty(await fixture.Context.InvoiceDetails.ToListAsync());
        Assert.Equal(700m, (await new InvoiceRepository(fixture.Context).GetAvailablePrepaidRentCreditAsync(1))!.AvailableAmount);
        Assert.Equal(1, await fixture.Context.Notifications.CountAsync()); // Checkout only.
    }

    [Fact]
    public async Task Mid_month_termination_preserves_issued_credit_and_historical_invoice_remains_payable()
    {
        await using var fixture = await CreditFixture.CreateAsync();
        var invoice = await fixture.Invoices.CreateAsync(new CreateInvoiceDto(1, StartMonth, Due), 7, RoleNames.Manager);
        var termination = await fixture.Contracts.TerminateContractAsync(1, new TerminateContractRequestDto(true, true, true),
            7, RoleNames.Manager, CancellationToken.None);
        Assert.Equal(200m, termination.OutstandingDebt);
        Assert.Equal("Terminated", termination.Contract.Status);
        await Assert.ThrowsAsync<InvoiceConflictException>(() => fixture.Invoices.CreateAsync(
            new CreateInvoiceDto(1, StartMonth, Due), 7, RoleNames.Manager));
        var paid = await fixture.Invoices.PayAsync(invoice.InvoiceId, new PayInvoiceDto("VNPay"), 10);
        Assert.Equal(("Paid", 700m, 200m), (paid.Status, paid.PrepaidRentApplied, paid.AmountPaid));
        Assert.Equal(new decimal[] { 200 }, fixture.Gateway.Charges);
        Assert.Equal(1, await fixture.Context.InvoiceDetails.CountAsync(row => row.ItemType == InvoiceItemTypes.PrepaidRentCredit));
        Assert.Null(await new InvoiceRepository(fixture.Context).GetAvailablePrepaidRentCreditAsync(1));
    }

    [Fact]
    public async Task Fractional_usage_and_half_dong_fixed_fee_round_per_line_before_credit_and_payment()
    {
        await using var fixture = await CreditFixture.CreateAsync(servicePrice: 0.5m);
        fixture.Context.Services.Add(new Service { ServiceId = 2, Name = "Electricity" });
        fixture.Context.RoomTypeServices.Add(new RoomTypeServices { RoomTypeId = 1, ServiceId = 2 });
        fixture.Context.PropertyServiceRates.Add(new PropertyServiceRates { RateId = 2, PropertyId = 1, ServiceId = 2,
            Price = 3333m, UnitCode = "kwh", BillingBasis = "usage", EffectiveFrom = new DateOnly(2000, 1, 1) });
        fixture.Context.UtilityReadings.Add(new UtilityReadings { ContractId = 1, ReadingMonth = StartMonth,
            ElectricStart = 100, ElectricEnd = 101.5m, WaterStart = 0, WaterEnd = 0 });
        await fixture.Context.SaveChangesAsync();
        var invoice = await fixture.Invoices.CreateAsync(new CreateInvoiceDto(1, StartMonth, Due), 7, RoleNames.Manager);
        Assert.Equal(5000m, Assert.Single(invoice.Lines, row => row.ItemType == InvoiceItemTypes.Electricity).Amount);
        Assert.Equal(1m, Assert.Single(invoice.Lines, row => row.ItemType == InvoiceItemTypes.Service).Amount);
        Assert.Equal(-700m, Assert.Single(invoice.Lines, row => row.ItemType == InvoiceItemTypes.PrepaidRentCredit).Amount);
        Assert.Equal(5001m, invoice.Total);
        await fixture.Invoices.PayAsync(invoice.InvoiceId, new PayInvoiceDto("MoMo"), 10);
        Assert.Equal(new decimal[] { 5001 }, fixture.Gateway.Charges);
    }

    [Fact]
    public async Task Failed_or_other_customers_payments_do_not_inflate_available_credit()
    {
        await using var fixture = await CreditFixture.CreateAsync();
        await fixture.Context.Payments.Where(row => row.BookingId == 1)
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Amount, 350));
        fixture.Context.Payments.AddRange(
            new Payments { UserId = 10, BookingId = 1, Amount = 350, Status = "Failed", PaymentMethod = "MoMo" },
            new Payments { UserId = 20, BookingId = 1, Amount = 350, Status = "Success", PaymentMethod = "MoMo" });
        await fixture.Context.SaveChangesAsync();
        var invoice = await fixture.Invoices.CreateAsync(new CreateInvoiceDto(1, StartMonth, Due), 7, RoleNames.Manager);
        Assert.Equal((350m, 550m), (invoice.PrepaidRentApplied, invoice.Total));
    }

    private sealed class CreditFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public AppDbContext Context { get; }
        public RecordingGateway Gateway { get; } = new();
        public InvoiceService Invoices { get; }
        public ContractService Contracts { get; }
        private CreditFixture(SqliteConnection connection, AppDbContext context)
        {
            _connection = connection;
            Context = context;
            var clock = new FixedClock();
            Invoices = new InvoiceService(new InvoiceRepository(context), Gateway, clock, new ScopeBNoCashCheckout());
            Contracts = new ContractService(new ContractRepository(context), clock);
        }

        public static async Task<CreditFixture> CreateAsync(decimal deposit = 700, decimal rent = 700, decimal servicePrice = 200)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
            var fixture = new CreditFixture(connection, context);
            try
            {
                await context.Database.EnsureCreatedAsync();
                context.Roles.AddRange(new Roles { RoleId = 1, RoleName = "Customer" },
                    new Roles { RoleId = 2, RoleName = "Manager" }, new Roles { RoleId = 3, RoleName = "Admin" });
                context.Users.AddRange(User(7, 2), User(9, 3), User(10, 1), User(20, 1));
                context.Properties.Add(new Property { PropertyId = 1, OwnerId = 9, PropertyName = "Credit review" });
                context.RoomTypes.Add(new RoomTypes { RoomTypeId = 1, TypeName = "Review", Price = rent });
                context.PropertyRoomTypes.Add(new PropertyRoomTypes { PropertyId = 1, RoomTypeId = 1, MonthlyRent = rent, IsActive = true });
                context.Rooms.Add(new Rooms { RoomId = 1, PropertyId = 1, RoomTypeId = 1, RoomCode = "R1", Status = "Occupied" });
                context.PropertyManagerAssignments.Add(new PropertyManagerAssignment { PropertyId = 1, ManagerId = 7 });
                context.Bookings.Add(new Bookings { BookingId = 1, RoomId = 1, CustomerId = 10, DepositAmount = deposit,
                    Status = "Deposited", ExpiryDate = new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc) });
                context.Payments.Add(new Payments { BookingId = 1, UserId = 10, Amount = deposit, Status = "Success", PaymentMethod = "MoMo" });
                context.Contracts.Add(new Contracts { ContractId = 1, BookingId = 1, RoomId = 1, CustomerId = 10,
                    ProcessedByStaffId = 7, StartDate = StartMonth, EndDate = new DateOnly(2027, 10, 31),
                    MonthlyRent = rent, DepositAmount = deposit, Status = "Active" });
                context.Services.Add(new Service { ServiceId = 1, Name = "Internet" });
                context.RoomTypeServices.Add(new RoomTypeServices { RoomTypeId = 1, ServiceId = 1 });
                context.PropertyServiceRates.Add(new PropertyServiceRates { RateId = 1, PropertyId = 1, ServiceId = 1,
                    Price = servicePrice, UnitCode = "room_month", BillingBasis = "fixed_room", EffectiveFrom = new DateOnly(2000, 1, 1) });
                await context.SaveChangesAsync();
                return fixture;
            }
            catch { await fixture.DisposeAsync(); throw; }
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }

        private static Users User(int id, int roleId) => new() { UserId = id, RoleId = roleId, FullName = "Review " + id,
            Email = id + "@credit-review.invalid", PasswordHash = "sqlite-test-only", IsActive = true };
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 11, 5, 0, 0, TimeSpan.Zero);
    }

    private sealed class RecordingGateway : IPaymentGateway
    {
        public List<decimal> Charges { get; } = [];
        public Task<bool> ChargeAsync(string paymentMethod, decimal amount)
        {
            Charges.Add(amount);
            return Task.FromResult(true);
        }
    }
}
