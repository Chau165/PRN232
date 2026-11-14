using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PRN232.Application.ReadModels;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using PRN232.Infrastructure.Repositories;
using Xunit;

namespace PRN232.Application.Tests;

public sealed class InvoiceRepositoryTests
{
    [Fact]
    public async Task AddIfAbsent_persists_booking_credit_as_a_negative_invoice_detail()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        await SeedAsync(context);
        var repository = new InvoiceRepository(context);

        var availableCredit = await repository.GetAvailablePrepaidRentCreditAsync(1);
        var invoice = NewInvoice();
        var added = await repository.AddIfAbsentAsync(
            invoice,
            new Notifications { UserId = 10, Title = "Invoice", Type = "Invoice" },
            managerId: 7);

        Assert.Equal(new PrepaidRentCreditReadModel(700m), availableCredit);
        Assert.True(added);
        Assert.Equal(InvoiceStatuses.Unpaid, invoice.Status);
        Assert.Equal(-700m, await context.InvoiceDetails
            .Where(detail => detail.ItemType == InvoiceItemTypes.PrepaidRentCredit)
            .Select(detail => detail.Amount)
            .SingleAsync());
        Assert.Null(await repository.GetAvailablePrepaidRentCreditAsync(1));
    }

    [Fact]
    public async Task AddIfAbsent_rechecks_active_manager_assignment_before_writing()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        await SeedAsync(context);
        context.PropertyManagerAssignments.Remove(await context.PropertyManagerAssignments.SingleAsync());
        await context.SaveChangesAsync();

        var added = await new InvoiceRepository(context).AddIfAbsentAsync(
            NewInvoice(),
            new Notifications { UserId = 10, Title = "Invoice", Type = "Invoice" },
            managerId: 7);

        Assert.False(added);
        Assert.Empty(await context.Invoices.ToListAsync());
        Assert.Empty(await context.Notifications.ToListAsync());
    }

    [Fact]
    public async Task AddIfAbsent_rechecks_contract_status_before_writing()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        await SeedAsync(context);
        var contract = await context.Contracts.SingleAsync();
        contract.Status = "Ended";
        await context.SaveChangesAsync();

        var added = await new InvoiceRepository(context).AddIfAbsentAsync(
            NewInvoice(),
            new Notifications { UserId = 10, Title = "Invoice", Type = "Invoice" },
            managerId: 7);

        Assert.False(added);
        Assert.Empty(await context.Invoices.ToListAsync());
        Assert.Empty(await context.Notifications.ToListAsync());
    }

    [Fact]
    public async Task AddIfAbsent_rejects_a_credit_amount_that_is_not_the_available_start_month_credit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        await SeedAsync(context);
        var invoice = NewInvoice();
        invoice.InvoiceDetails.Single(detail => detail.ItemType == InvoiceItemTypes.PrepaidRentCredit).Amount = -350m;

        var added = await new InvoiceRepository(context).AddIfAbsentAsync(
            invoice,
            new Notifications { UserId = 10, Title = "Invoice", Type = "Invoice" },
            managerId: 7);

        Assert.False(added);
        Assert.Empty(await context.Invoices.ToListAsync());
        Assert.Empty(await context.Notifications.ToListAsync());
    }

    [Fact]
    public async Task AddIfAbsent_serializes_parallel_requests_and_commits_only_one_invoice_and_credit()
    {
        const string connectionString = "Data Source=InvoiceRepositoryConcurrency;Mode=Memory;Cache=Shared;Default Timeout=10";
        await using var keepAlive = new SqliteConnection(connectionString);
        await keepAlive.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).Options;
        await using (var setup = new AppDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            await SeedAsync(setup);
        }

        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<bool> AddAsync()
        {
            await using var context = new AppDbContext(options);
            await start.Task;
            return await new InvoiceRepository(context).AddIfAbsentAsync(
                NewInvoice(),
                new Notifications { UserId = 10, Title = "Invoice", Type = "Invoice" },
                managerId: 7);
        }

        var first = AddAsync();
        var second = AddAsync();
        start.SetResult();
        var results = await Task.WhenAll(first, second);

        Assert.Single(results, added => added);
        await using var verify = new AppDbContext(options);
        Assert.Equal(1, await verify.Invoices.CountAsync());
        Assert.Equal(3, await verify.InvoiceDetails.CountAsync());
        Assert.Equal(1, await verify.InvoiceDetails.CountAsync(detail => detail.ItemType == InvoiceItemTypes.PrepaidRentCredit));
        Assert.Equal(1, await verify.Notifications.CountAsync(notification => notification.Type == "Invoice"));
        Assert.Equal(0, await verify.Payments.CountAsync(payment => payment.InvoiceId != null));
    }

    [Fact]
    public async Task TryPayOnline_serializes_before_gateway_and_records_one_net_payment()
    {
        const string connectionString = "Data Source=InvoicePaymentConcurrency;Mode=Memory;Cache=Shared;Default Timeout=10";
        await using var keepAlive = new SqliteConnection(connectionString);
        await keepAlive.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).Options;
        int invoiceId;
        await using (var setup = new AppDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            await SeedAsync(setup);
            var invoice = NewInvoice();
            Assert.True(await new InvoiceRepository(setup).AddIfAbsentAsync(invoice,
                new Notifications { UserId = 10, Title = "Created", Type = "Invoice" }, managerId: 7));
            invoiceId = invoice.InvoiceId;
        }

        var chargeCalls = 0;
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<InvoicePaymentAttemptStatus> PayAsync()
        {
            await using var context = new AppDbContext(options);
            await start.Task;
            return await new InvoiceRepository(context).TryPayOnlineAsync(
                invoiceId,
                customerId: 10,
                PaymentMethods.VNPay,
                amount =>
                {
                    Interlocked.Increment(ref chargeCalls);
                    Assert.Equal(200m, amount);
                    return Task.FromResult(true);
                },
                new Notifications { UserId = 9, Title = "Paid", Type = "Invoice" });
        }

        var first = PayAsync();
        var second = PayAsync();
        start.SetResult();
        var results = await Task.WhenAll(first, second);

        Assert.Single(results, result => result == InvoicePaymentAttemptStatus.Completed);
        Assert.Single(results, result => result == InvoicePaymentAttemptStatus.Conflict);
        Assert.Equal(1, chargeCalls);
        await using var verify = new AppDbContext(options);
        var payment = await verify.Payments.SingleAsync(item => item.InvoiceId == invoiceId);
        Assert.Equal((10, 200m, PaymentStatuses.Success), (payment.UserId, payment.Amount, payment.Status));
        Assert.Equal(2, await verify.Notifications.CountAsync(notification => notification.Type == "Invoice"));
    }

    [Fact]
    public async Task MarkPaid_serializes_parallel_cash_recordings_and_persists_one_payment()
    {
        const string connectionString = "Data Source=InvoiceCashConcurrency;Mode=Memory;Cache=Shared;Default Timeout=10";
        await using var keepAlive = new SqliteConnection(connectionString);
        await keepAlive.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).Options;
        int invoiceId;
        await using (var setup = new AppDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            await SeedAsync(setup);
            var invoice = NewInvoice();
            Assert.True(await new InvoiceRepository(setup).AddIfAbsentAsync(invoice,
                new Notifications { UserId = 10, Title = "Created", Type = "Invoice" }, managerId: 7));
            invoiceId = invoice.InvoiceId;
        }

        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<bool> RecordCashAsync()
        {
            await using var context = new AppDbContext(options);
            await start.Task;
            return await new InvoiceRepository(context).MarkPaidAsync(
                invoiceId,
                new Payments
                {
                    UserId = 10,
                    InvoiceId = invoiceId,
                    Amount = 200m,
                    PaymentMethod = PaymentMethods.Cash,
                    Status = PaymentStatuses.Success
                },
                new Notifications { UserId = 10, Title = "Cash received", Type = "Invoice" },
                managerId: 7);
        }

        var first = RecordCashAsync();
        var second = RecordCashAsync();
        start.SetResult();
        var results = await Task.WhenAll(first, second);

        Assert.Single(results, added => added);
        await using var verify = new AppDbContext(options);
        var payment = await verify.Payments.SingleAsync(item => item.InvoiceId == invoiceId);
        Assert.Equal((200m, PaymentMethods.Cash, PaymentStatuses.Success), (payment.Amount, payment.PaymentMethod, payment.Status));
        Assert.Equal(2, await verify.Notifications.CountAsync(notification => notification.Type == "Invoice"));
    }

    [Fact]
    public async Task AddIfAbsent_marks_fully_credited_invoice_paid_without_a_fake_payment()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        await SeedAsync(context);
        var invoice = NewInvoice();
        invoice.InvoiceDetails.Single(detail => detail.ItemType == InvoiceItemTypes.Service).Amount = 0m;

        Assert.True(await new InvoiceRepository(context).AddIfAbsentAsync(invoice,
            new Notifications { UserId = 10, Title = "Paid by credit", Type = "Invoice" }, managerId: 7));

        Assert.Equal(InvoiceStatuses.Paid, invoice.Status);
        Assert.Equal(0m, invoice.InvoiceDetails.Sum(detail => detail.Amount));
        Assert.Empty(await context.Payments.Where(payment => payment.InvoiceId == invoice.InvoiceId).ToListAsync());
    }

    [Fact]
    public async Task AddIfAbsent_accepts_a_later_month_first_then_applies_credit_only_to_the_start_month()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        await SeedAsync(context);
        var repository = new InvoiceRepository(context);

        var laterMonth = NewInvoiceWithoutCredit(new DateOnly(2026, 12, 1));
        var firstMonth = NewInvoice();
        Assert.True(await repository.AddIfAbsentAsync(laterMonth,
            new Notifications { UserId = 10, Title = "December", Type = "Invoice" }, managerId: 7));
        Assert.True(await repository.AddIfAbsentAsync(firstMonth,
            new Notifications { UserId = 10, Title = "November", Type = "Invoice" }, managerId: 7));
        Assert.False(await repository.AddIfAbsentAsync(NewInvoice(),
            new Notifications { UserId = 10, Title = "Duplicate", Type = "Invoice" }, managerId: 7));

        Assert.DoesNotContain(laterMonth.InvoiceDetails, detail => detail.ItemType == InvoiceItemTypes.PrepaidRentCredit);
        Assert.Equal(-700m, (await context.InvoiceDetails.SingleAsync(detail =>
            detail.InvoiceId == firstMonth.InvoiceId && detail.ItemType == InvoiceItemTypes.PrepaidRentCredit)).Amount);
        Assert.Null(await repository.GetAvailablePrepaidRentCreditAsync(1));
        Assert.Equal(2, await context.Notifications.CountAsync(notification => notification.Type == "Invoice"));
    }

    private static async Task SeedAsync(AppDbContext context)
    {
        context.Roles.AddRange(
            new Roles { RoleId = 1, RoleName = RoleNames.Customer },
            new Roles { RoleId = 2, RoleName = RoleNames.Manager },
            new Roles { RoleId = 3, RoleName = RoleNames.Admin });
        context.Users.AddRange(
            User(7, 2, "Manager"),
            User(9, 3, "Admin"),
            User(10, 1, "Customer"));
        context.Properties.Add(new Property { PropertyId = 1, OwnerId = 9, PropertyName = "Sunrise" });
        context.RoomTypes.Add(new RoomTypes { RoomTypeId = 1, TypeName = "Studio", Price = 700m });
        context.PropertyRoomTypes.Add(new PropertyRoomTypes { PropertyId = 1, RoomTypeId = 1, MonthlyRent = 700m, IsActive = true });
        context.Rooms.Add(new Rooms { RoomId = 1, PropertyId = 1, RoomTypeId = 1, RoomCode = "A101" });
        context.Bookings.Add(new Bookings
        {
            BookingId = 1,
            RoomId = 1,
            CustomerId = 10,
            DepositAmount = 700m,
            Status = BookingStatuses.Deposited,
            ExpiryDate = DateTime.UtcNow.AddDays(7)
        });
        context.Contracts.Add(new Contracts
        {
            ContractId = 1,
            BookingId = 1,
            RoomId = 1,
            CustomerId = 10,
            StartDate = new DateOnly(2026, 11, 1),
            EndDate = new DateOnly(2027, 10, 31),
            MonthlyRent = 700m,
            DepositAmount = 700m,
            Status = "Active"
        });
        context.PropertyManagerAssignments.Add(new PropertyManagerAssignment
        {
            PropertyId = 1,
            ManagerId = 7
        });
        context.Payments.AddRange(
            Payment(1, 300m),
            Payment(2, 400m));
        await context.SaveChangesAsync();
    }

    private static Invoices NewInvoice()
    {
        var invoice = new Invoices
        {
            ContractId = 1,
            InvoiceMonth = new DateOnly(2026, 11, 1),
            DueDate = new DateOnly(2026, 12, 1),
            Status = InvoiceStatuses.Unpaid
        };
        invoice.InvoiceDetails.Add(new InvoiceDetails
        {
            ItemType = InvoiceItemTypes.Rent,
            Description = "Monthly rent",
            Amount = 700m
        });
        invoice.InvoiceDetails.Add(new InvoiceDetails
        {
            ItemType = InvoiceItemTypes.Service,
            Description = "Service fee",
            Amount = 200m
        });
        invoice.InvoiceDetails.Add(new InvoiceDetails
        {
            ItemType = InvoiceItemTypes.PrepaidRentCredit,
            Description = "Prepaid Booking deposit",
            Amount = -700m
        });
        return invoice;
    }

    private static Invoices NewInvoiceWithoutCredit(DateOnly month)
    {
        var invoice = new Invoices
        {
            ContractId = 1,
            InvoiceMonth = month,
            DueDate = month.AddMonths(1),
            Status = InvoiceStatuses.Unpaid
        };
        invoice.InvoiceDetails.Add(new InvoiceDetails
        {
            ItemType = InvoiceItemTypes.Rent,
            Description = "Monthly rent",
            Amount = 700m
        });
        invoice.InvoiceDetails.Add(new InvoiceDetails
        {
            ItemType = InvoiceItemTypes.Service,
            Description = "Service fee",
            Amount = 200m
        });
        return invoice;
    }

    private static Payments Payment(int id, decimal amount) => new()
    {
        PaymentId = id,
        UserId = 10,
        BookingId = 1,
        Amount = amount,
        PaymentMethod = "VNPay",
        Status = PaymentStatuses.Success
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
}
