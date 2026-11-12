using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PRN232.Application.ReadModels;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using PRN232.Infrastructure.Repositories;
using Xunit;

namespace PRN232.Application.Tests;

public sealed class ContractBillingTimelineRepositoryTests
{
    [Fact]
    public async Task GetIssuedInvoicesForTimelineAsync_returns_empty_when_contract_has_no_invoices()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        await SeedContractsAsync(context);

        var invoices = await new ContractRepository(context)
            .GetIssuedInvoicesForTimelineAsync(1, CancellationToken.None);

        Assert.Empty(invoices);
    }

    [Fact]
    public async Task GetIssuedInvoicesForTimelineAsync_projects_totals_credit_and_only_successful_payments_for_contract_invoices()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var commandCounter = new ReaderCommandCounter();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(commandCounter)
            .Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        await SeedContractsAsync(context);
        context.Invoices.AddRange(
            CreateInvoice(101, 1, new DateOnly(2026, 11, 1), InvoiceStatuses.Unpaid,
                (InvoiceItemTypes.Rent, 1_000m),
                (InvoiceItemTypes.Service, 500m),
                (InvoiceItemTypes.PrepaidRentCredit, -200m)),
            CreateInvoice(102, 1, new DateOnly(2026, 12, 1), InvoiceStatuses.Paid,
                (InvoiceItemTypes.Rent, 2_000m),
                (InvoiceItemTypes.PrepaidRentCredit, -300m)),
            CreateInvoice(201, 2, new DateOnly(2026, 11, 1), InvoiceStatuses.Paid,
                (InvoiceItemTypes.Rent, 800m)));
        context.Payments.AddRange(
            CreatePayment(11, 101, 100m, PaymentStatuses.Success, "VNPay"),
            CreatePayment(12, 101, 150m, PaymentStatuses.Success, "MoMo"),
            CreatePayment(13, 101, 70m, "Failed", "Cash"),
            CreatePayment(21, 102, 700m, PaymentStatuses.Success, "VNPay"),
            CreatePayment(22, 102, 1_000m, PaymentStatuses.Success, "Cash"),
            CreatePayment(31, 201, 800m, PaymentStatuses.Success, "MoMo", userId: 20));
        await context.SaveChangesAsync();
        commandCounter.Reset();

        var invoices = await new ContractRepository(context)
            .GetIssuedInvoicesForTimelineAsync(1, CancellationToken.None);

        Assert.Equal(2, commandCounter.ReaderCommandCount);
        Assert.Collection(
            invoices,
            unpaid =>
            {
                Assert.Equal(101, unpaid.InvoiceId);
                Assert.Equal(1_300m, unpaid.Total);
                Assert.Equal(200m, unpaid.PrepaidRentApplied);
                Assert.Equal(1_300m, unpaid.NetPayable);
                Assert.Equal(250m, unpaid.AmountPaid);
                Assert.Equal(1_050m, unpaid.RemainingPayable);
                Assert.Equal("MoMo", unpaid.PaymentMethod);
            },
            paid =>
            {
                Assert.Equal(102, paid.InvoiceId);
                Assert.Equal(1_700m, paid.Total);
                Assert.Equal(300m, paid.PrepaidRentApplied);
                Assert.Equal(1_700m, paid.NetPayable);
                Assert.Equal(1_700m, paid.AmountPaid);
                Assert.Equal(0m, paid.RemainingPayable);
                Assert.Equal("Cash", paid.PaymentMethod);
            });
    }

    private static async Task SeedContractsAsync(AppDbContext context)
    {
        context.Roles.AddRange(
            new Roles { RoleId = 1, RoleName = RoleNames.Customer },
            new Roles { RoleId = 3, RoleName = RoleNames.Admin });
        context.Users.AddRange(
            User(9, 3, "Property Owner"),
            User(10, 1, "Customer A"),
            User(20, 1, "Customer B"));
        context.Properties.Add(new Property
        {
            PropertyId = 1,
            OwnerId = 9,
            PropertyName = "Sunrise"
        });
        context.RoomTypes.Add(new RoomTypes
        {
            RoomTypeId = 1,
            TypeName = "Studio",
            Price = 1_000m
        });
        context.Rooms.AddRange(
            new Rooms { RoomId = 1, PropertyId = 1, RoomTypeId = 1, RoomCode = "A101" },
            new Rooms { RoomId = 2, PropertyId = 1, RoomTypeId = 1, RoomCode = "A102" });
        context.Contracts.AddRange(
            Contract(1, 1, 10),
            Contract(2, 2, 20));
        await context.SaveChangesAsync();
    }

    private static Contracts Contract(int id, int roomId, int customerId) => new()
    {
        ContractId = id,
        RoomId = roomId,
        CustomerId = customerId,
        StartDate = new DateOnly(2026, 11, 1),
        EndDate = new DateOnly(2027, 10, 31),
        MonthlyRent = 1_000m,
        DepositAmount = 1_000m,
        Status = ContractStatuses.Active
    };

    private static Invoices CreateInvoice(
        int invoiceId,
        int contractId,
        DateOnly month,
        string status,
        params (string ItemType, decimal Amount)[] lines)
    {
        var invoice = new Invoices
        {
            InvoiceId = invoiceId,
            ContractId = contractId,
            InvoiceMonth = month,
            DueDate = month.AddMonths(1),
            Status = status
        };
        foreach (var (itemType, amount) in lines)
        {
            invoice.InvoiceDetails.Add(new InvoiceDetails
            {
                ItemType = itemType,
                Description = itemType,
                Amount = amount
            });
        }

        return invoice;
    }

    private static Payments CreatePayment(
        int id,
        int invoiceId,
        decimal amount,
        string status,
        string method,
        int userId = 10) => new()
    {
        PaymentId = id,
        UserId = userId,
        InvoiceId = invoiceId,
        Amount = amount,
        Status = status,
        PaymentMethod = method
    };

    private static Users User(int id, int roleId, string name) => new()
    {
        UserId = id,
        RoleId = roleId,
        FullName = name,
        Email = $"{id}@example.test",
        PasswordHash = "test",
        IsActive = true
    };

    private sealed class ReaderCommandCounter : DbCommandInterceptor
    {
        private int _readerCommandCount;

        public int ReaderCommandCount => Volatile.Read(ref _readerCommandCount);

        public void Reset()
        {
            Interlocked.Exchange(ref _readerCommandCount, 0);
        }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            Interlocked.Increment(ref _readerCommandCount);
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _readerCommandCount);
            return ValueTask.FromResult(result);
        }
    }
}
