using System.Data.Common;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.Services;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using PRN232.Infrastructure.Repositories;
using Xunit;

namespace PRN232.Application.Tests;

public sealed class ScopeBPostgreSqlFactAttribute : FactAttribute
{
    public ScopeBPostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ScopeBReviewPostgres.EnvironmentName)))
            Skip = "Requires the explicitly configured isolated loopback PostgreSQL test instance.";
    }
}

public sealed class ScopeBPostgreSqlTheoryAttribute : TheoryAttribute
{
    public ScopeBPostgreSqlTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ScopeBReviewPostgres.EnvironmentName)))
            Skip = "Requires the explicitly configured isolated loopback PostgreSQL test instance.";
    }
}

public class ScopeBPostgreSqlConcurrencyReviewTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(20);
    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(
        DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")).DateTime);
    private static DateOnly Month => new(Today.Year, Today.Month, 1);

    [ScopeBPostgreSqlFact]
    public async Task Two_customers_wait_on_room_lock_and_only_one_booking_is_committed()
    {
        await using var db = await ScopeBReviewPostgres.CreateAsync();
        var gate = new ScopeBSqlPause("INSERT INTO \"Bookings\"");
        await using var first = db.Context(gate);
        await using var second = db.Context();
        var winner = Attempt(() => new BookingService(new BookingRepository(first), new CountingGateway(), TimeProvider.System)
            .CreateAsync(new CreateBookingDto(1), 10));
        await gate.Entered.WaitAsync(Deadline);
        var loser = Attempt(() => new BookingService(new BookingRepository(second), new CountingGateway(), TimeProvider.System)
            .CreateAsync(new CreateBookingDto(1), 20));
        try { await db.AssertBlockedByAsync(first); }
        finally { gate.Release(); }
        Assert.Null(await winner.WaitAsync(Deadline));
        Assert.IsType<BookingRoomUnavailableException>(await loser.WaitAsync(Deadline));
        await using var verify = db.Context();
        var booking = await verify.Bookings.SingleAsync();
        Assert.Equal((10, "Pending"), (booking.CustomerId, booking.Status));
        Assert.Equal(1, await verify.Notifications.CountAsync());
    }

    [ScopeBPostgreSqlFact]
    public async Task Same_deposited_booking_serializes_contract_creation_and_has_one_contract()
    {
        await using var db = await ScopeBReviewPostgres.CreateAsync();
        await db.SeedPaidBookingAsync();
        var gate = new ScopeBSqlPause("INSERT INTO \"Contracts\"");
        await using var first = db.Context(gate);
        await using var second = db.Context();
        var dto = new CreateContractDto(1, Today, Today.AddYears(1));
        var winner = Attempt(() => new ContractService(new ContractRepository(first), TimeProvider.System)
            .CreateFromBookingAsync(dto, 7, RoleNames.Manager));
        await gate.Entered.WaitAsync(Deadline);
        var loser = Attempt(() => new ContractService(new ContractRepository(second), TimeProvider.System)
            .CreateFromBookingAsync(dto, 7, RoleNames.Manager));
        try { await db.AssertBlockedByAsync(first); }
        finally { gate.Release(); }
        Assert.Null(await winner.WaitAsync(Deadline));
        Assert.IsType<ContractConflictException>(await loser.WaitAsync(Deadline));
        await using var verify = db.Context();
        Assert.Equal(1, await verify.Contracts.CountAsync());
        Assert.Equal("Occupied", (await verify.Rooms.SingleAsync()).Status);
        Assert.Equal(1, await verify.Notifications.CountAsync());
    }

    [ScopeBPostgreSqlTheory]
    [InlineData("online", "online")]
    [InlineData("cash", "cash")]
    [InlineData("online", "cash")]
    [InlineData("cash", "online")]
    public async Task Duplicate_invoice_payment_waits_and_records_exactly_one_net_payment(string firstMethod, string secondMethod)
    {
        await using var db = await ScopeBReviewPostgres.CreateAsync();
        await db.SeedContractAndInvoiceAsync();
        var gate = new ScopeBSqlPause("UPDATE \"Invoices\"");
        var gateway = new CountingGateway();
        await using var first = db.Context(gate);
        await using var second = db.Context();
        var winner = Attempt(() => PayAsync(first, gateway, firstMethod));
        await gate.Entered.WaitAsync(Deadline);
        var loser = Attempt(() => PayAsync(second, gateway, secondMethod));
        try { await db.AssertBlockedByAsync(first); }
        finally { gate.Release(); }
        Assert.Null(await winner.WaitAsync(Deadline));
        Assert.IsType<InvoiceConflictException>(await loser.WaitAsync(Deadline));
        await using var verify = db.Context();
        var payment = Assert.Single(await verify.Payments.Where(row => row.InvoiceId == 1).ToListAsync());
        Assert.Equal((200m, firstMethod == "cash" ? "Cash" : "MoMo"), (payment.Amount, payment.PaymentMethod));
        Assert.Equal("Paid", (await verify.Invoices.SingleAsync()).Status);
        Assert.Equal(1, await verify.Notifications.CountAsync());
        Assert.Equal(firstMethod == "online" ? 1 : 0, gateway.Calls);
    }

    [ScopeBPostgreSqlFact]
    public async Task Parallel_start_month_invoices_lock_contract_and_consume_credit_once()
    {
        await using var db = await ScopeBReviewPostgres.CreateAsync();
        await db.SeedContractAndInvoiceAsync();
        await using (var prepare = db.Context()) await prepare.Invoices.ExecuteDeleteAsync();
        var gate = new ScopeBSqlPause("INSERT INTO \"Invoices\"");
        await using var first = db.Context(gate);
        await using var second = db.Context();
        var request = new CreateInvoiceDto(1, Month, Today.AddDays(7));
        var winner = Attempt(() => new InvoiceService(new InvoiceRepository(first), new CountingGateway(), TimeProvider.System)
            .CreateAsync(request, 7, RoleNames.Manager));
        await gate.Entered.WaitAsync(Deadline);
        var loser = Attempt(() => new InvoiceService(new InvoiceRepository(second), new CountingGateway(), TimeProvider.System)
            .CreateAsync(request, 7, RoleNames.Manager));
        try { await db.AssertBlockedByAsync(first); }
        finally { gate.Release(); }
        Assert.Null(await winner.WaitAsync(Deadline));
        Assert.IsType<InvoiceConflictException>(await loser.WaitAsync(Deadline));
        await using var verify = db.Context();
        Assert.Equal("Paid", (await verify.Invoices.SingleAsync()).Status);
        Assert.Equal(-700m, (await verify.InvoiceDetails.SingleAsync(row => row.ItemType == InvoiceItemTypes.PrepaidRentCredit)).Amount);
        Assert.Equal(0m, (await verify.InvoiceDetails.Select(row => row.Amount).ToListAsync()).Sum());
        Assert.Empty(await verify.Payments.Where(row => row.InvoiceId != null).ToListAsync());
        Assert.Equal(1, await verify.Notifications.CountAsync());
    }

    [ScopeBPostgreSqlFact]
    public async Task Termination_waits_for_inflight_online_payment_then_computes_zero_debt_without_deadlock()
    {
        await using var db = await ScopeBReviewPostgres.CreateAsync();
        await db.SeedContractAndInvoiceAsync();
        var gateway = new CountingGateway { Pause = new ScopeBAsyncGate() };
        await using var paying = db.Context();
        await using var ending = db.Context();
        var payment = Attempt(() => PayAsync(paying, gateway, "online"));
        await gateway.Pause.Entered.WaitAsync(Deadline);
        var termination = new ContractRepository(ending).TerminateAsync(1, 7, true, false,
            DateTimeOffset.UtcNow, Notice(10), CancellationToken.None);
        try { await db.AssertBlockedByAsync(paying); }
        finally { gateway.Pause.Release(); }
        Assert.Null(await payment.WaitAsync(Deadline));
        var result = await termination.WaitAsync(Deadline);
        Assert.Equal(ContractMutationStatus.Updated, result.Status);
        Assert.Equal(0m, result.OutstandingDebt);
        await VerifyEndedAndPaidAsync(db);
        Assert.Equal(1, gateway.Calls);
    }

    [ScopeBPostgreSqlFact]
    public async Task Payment_waits_for_termination_and_can_settle_the_remaining_historical_invoice()
    {
        await using var db = await ScopeBReviewPostgres.CreateAsync();
        await db.SeedContractAndInvoiceAsync();
        var gate = new ScopeBSqlPause("UPDATE \"Contracts\"");
        await using var ending = db.Context(gate);
        await using var paying = db.Context();
        var termination = new ContractRepository(ending).TerminateAsync(1, 7, true, true,
            DateTimeOffset.UtcNow, Notice(10), CancellationToken.None);
        await gate.Entered.WaitAsync(Deadline);
        var payment = Attempt(() => PayAsync(paying, new CountingGateway(), "online"));
        try { await db.AssertBlockedByAsync(ending); }
        finally { gate.Release(); }
        Assert.Equal(ContractMutationStatus.Updated, (await termination.WaitAsync(Deadline)).Status);
        Assert.Null(await payment.WaitAsync(Deadline));
        await VerifyEndedAndPaidAsync(db);
    }

    [ScopeBPostgreSqlFact]
    public async Task Revocation_waits_for_manager_shared_assignment_lock_then_prevents_new_writes()
    {
        await using var db = await ScopeBReviewPostgres.CreateAsync();
        await db.SeedContractAndInvoiceAsync();
        var gate = new ScopeBSqlPause("INSERT INTO \"UtilityReadings\"");
        await using var writing = db.Context(gate);
        await using var revoking = db.Context();
        var write = Attempt(() => new UtilityReadingService(new UtilityReadingRepository(writing), TimeProvider.System)
            .CreateAsync(Reading(), 7, RoleNames.Manager));
        await gate.Entered.WaitAsync(Deadline);
        var revoke = new PropertyManagerAssignmentRepository(revoking)
            .RevokeActiveAsync(1, 7, 9, CancellationToken.None);
        try { await db.AssertBlockedByAsync(writing); }
        finally { gate.Release(); }
        Assert.Null(await write.WaitAsync(Deadline));
        Assert.Equal(RevokePropertyManagerAssignmentResult.Revoked, await revoke.WaitAsync(Deadline));
        await using var verify = db.Context();
        Assert.Equal(1, await verify.UtilityReadings.CountAsync());
        Assert.Empty(await verify.PropertyManagerAssignments.ToListAsync());
        Assert.IsType<UtilityReadingAuthorizationException>(await Attempt(() =>
            new UtilityReadingService(new UtilityReadingRepository(verify), TimeProvider.System)
                .CreateAsync(Reading(), 7, RoleNames.Manager)));
    }

    [ScopeBPostgreSqlFact]
    public async Task Manager_write_waits_for_revocation_and_rechecks_assignment_after_the_lock()
    {
        await using var db = await ScopeBReviewPostgres.CreateAsync();
        await db.SeedContractAndInvoiceAsync();
        var gate = new ScopeBSqlPause("DELETE FROM \"PropertyManagerAssignments\"");
        await using var revoking = db.Context(gate);
        await using var writing = db.Context();
        var revoke = new PropertyManagerAssignmentRepository(revoking)
            .RevokeActiveAsync(1, 7, 9, CancellationToken.None);
        await gate.Entered.WaitAsync(Deadline);
        var write = Attempt(() => new UtilityReadingService(new UtilityReadingRepository(writing), TimeProvider.System)
            .CreateAsync(Reading(), 7, RoleNames.Manager));
        try { await db.AssertBlockedByAsync(revoking); }
        finally { gate.Release(); }
        Assert.Equal(RevokePropertyManagerAssignmentResult.Revoked, await revoke.WaitAsync(Deadline));
        // B-AUTH-03 is known: the repository blocks writing, but the service maps false to 409.
        Assert.IsType<UtilityReadingConflictException>(await write.WaitAsync(Deadline));
        await using var verify = db.Context();
        Assert.Empty(await verify.UtilityReadings.ToListAsync());
        Assert.Empty(await verify.PropertyManagerAssignments.ToListAsync());
        Assert.Empty(await verify.Notifications.ToListAsync());
    }

    [ScopeBPostgreSqlTheory]
    [InlineData("inactive")]
    [InlineData("role")]
    // Chính sách đã chọn (11/11/2026): request đã qua kiểm tra quyền thì được hoàn tất, request mới mới bị chặn.
    // Khoá/hạ role giữa lần kiểm tra cuối và INSERT không huỷ ghi đang chạy (cửa sổ mili-giây). Muốn chặn cả
    // trường hợp này phải khoá dòng Users trong mọi thao tác của Manager; nếu đổi chính sách thì đổi test này.
    public async Task Manager_permission_changed_after_last_check_does_not_stop_the_write_in_flight(string loss)
    {
        await using var db = await ScopeBReviewPostgres.CreateAsync();
        await db.SeedContractAndInvoiceAsync();
        var gate = new ScopeBSqlPause("INSERT INTO \"UtilityReadings\"");
        await using var writing = db.Context(gate);
        await using var administration = db.Context();
        var write = Attempt(() => new UtilityReadingService(new UtilityReadingRepository(writing), TimeProvider.System)
            .CreateAsync(Reading(), 7, RoleNames.Manager));
        await gate.Entered.WaitAsync(Deadline);
        try
        {
            var changed = loss == "inactive"
                ? await administration.Users.Where(row => row.UserId == 7)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.IsActive, false)).WaitAsync(Deadline)
                : await administration.Users.Where(row => row.UserId == 7)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.RoleId, 1)).WaitAsync(Deadline);
            Assert.Equal(1, changed); // Independent transaction has committed before the blocked INSERT resumes.
        }
        finally { gate.Release(); }
        var error = await write.WaitAsync(Deadline);
        await using var verify = db.Context();
        var manager = await verify.Users.SingleAsync(row => row.UserId == 7);
        Assert.True(loss == "inactive" ? !manager.IsActive : manager.RoleId == 1);
        var persisted = await verify.UtilityReadings.CountAsync();
        Assert.True(error is null && persisted == 1,
            $"Permission loss '{loss}' committed before INSERT resumed; expected the in-flight write to finish, but writer error={error?.GetType().Name ?? "none"}, persisted readings={persisted}.");
    }

    private static async Task VerifyEndedAndPaidAsync(ScopeBReviewPostgres db)
    {
        await using var verify = db.Context();
        Assert.Equal("Terminated", (await verify.Contracts.SingleAsync()).Status);
        Assert.Equal("Available", (await verify.Rooms.SingleAsync()).Status);
        Assert.Equal("Paid", (await verify.Invoices.SingleAsync()).Status);
        Assert.Equal(200m, (await verify.Payments.SingleAsync(row => row.InvoiceId == 1)).Amount);
        Assert.Equal(2, await verify.Notifications.CountAsync());
    }

    private static Task<InvoiceDto> PayAsync(AppDbContext context, CountingGateway gateway, string method)
    {
        var service = new InvoiceService(new InvoiceRepository(context), gateway, TimeProvider.System);
        return method == "cash" ? service.RecordCashPaymentAsync(1, 7, RoleNames.Manager)
            : service.PayAsync(1, new PayInvoiceDto("MoMo"), 10);
    }

    private static CreateUtilityReadingDto Reading() => new(1, Month, 150, 14, 100, 10);
    private static Notifications Notice(int userId) => new() { UserId = userId, Title = "Concurrency review", Type = "Invoice", IsRead = false };
    private static async Task<Exception?> Attempt(Func<Task> action)
    {
        try { await action(); return null; }
        catch (Exception error) { return error; }
    }

    private sealed class CountingGateway : IPaymentGateway
    {
        private int _calls;
        public int Calls => _calls;
        public ScopeBAsyncGate? Pause { get; init; }
        public async Task<bool> ChargeAsync(string paymentMethod, decimal amount)
        {
            Interlocked.Increment(ref _calls);
            if (Pause is not null) await Pause.WaitAsync();
            return true;
        }
    }
}

internal sealed class ScopeBAsyncGate
{
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task Entered => _entered.Task;
    public async Task WaitAsync()
    {
        _entered.TrySetResult();
        await _release.Task.WaitAsync(TimeSpan.FromSeconds(20));
    }
    public void Release() => _release.TrySetResult();
}

internal sealed class ScopeBSqlPause(string sql) : DbCommandInterceptor
{
    private readonly ScopeBAsyncGate _gate = new();
    private int _claimed;
    public Task Entered => _gate.Entered;
    public void Release() => _gate.Release();
    private async Task PauseAsync(DbCommand command)
    {
        if (command.CommandText.Contains(sql, StringComparison.Ordinal) && Interlocked.CompareExchange(ref _claimed, 1, 0) == 0)
            await _gate.WaitAsync();
    }
    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        await PauseAsync(command);
        return result;
    }
    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        await PauseAsync(command);
        return result;
    }
}

internal sealed class ScopeBReviewPostgres : IAsyncDisposable
{
    internal const string EnvironmentName = "PRN232_MIGRATION_TEST_POSTGRES_CONNECTION";
    private readonly string _server;
    private readonly string _database;
    private readonly string _connection;
    private ScopeBReviewPostgres(string server, string database)
    {
        _server = server;
        _database = database;
        _connection = new NpgsqlConnectionStringBuilder(server) { Database = database }.ConnectionString;
    }

    public AppDbContext Context(params IInterceptor[] interceptors) => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(_connection).AddInterceptors(interceptors).Options);

    public static async Task<ScopeBReviewPostgres> CreateAsync()
    {
        var builder = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable(EnvironmentName));
        Assert.True(builder.Host is "127.0.0.1" or "localhost" or "::1", "Only a single loopback host is allowed.");
        Assert.Equal(55432, builder.Port);
        Assert.Equal("postgres", builder.Database);
        Assert.Equal("PRN232-Phase31B-Isolated-Test", builder.ApplicationName);
        builder.Pooling = false;
        builder.Timeout = 5;
        builder.CommandTimeout = 15;
        var database = "prn232_codex_scopeb_" + Guid.NewGuid().ToString("N");
        var fixture = new ScopeBReviewPostgres(builder.ConnectionString, database);
        await using (var connection = new NpgsqlConnection(builder.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", connection);
            await command.ExecuteNonQueryAsync();
        }
        try
        {
            // The local vanilla server need not have optional hosted-service roles. REVOKE
            // from an absent role is a no-op semantically but PostgreSQL rejects its name.
            // Filter only those absent REVOKE targets, in this test connection, without
            // creating cluster-wide roles or changing any source migration/schema DDL.
            var compatibility = await ScopeBLocalRevokeCompatibility.CreateAsync(fixture._connection);
            await using var context = fixture.Context(compatibility);
            await context.Database.MigrateAsync();
            context.Users.AddRange(User(7, 2), User(9, 3), User(10, 1), User(20, 1));
            context.Properties.Add(new Property { PropertyId = 1, OwnerId = 9, PropertyName = "Isolated review" });
            context.RoomTypes.Add(new RoomTypes { RoomTypeId = 1, TypeName = "Review", Price = 700 });
            context.Rooms.Add(new Rooms { RoomId = 1, PropertyId = 1, RoomTypeId = 1, RoomCode = "R1", Status = "Available" });
            context.PropertyManagerAssignments.Add(new PropertyManagerAssignment { PropertyId = 1, ManagerId = 7 });
            await context.SaveChangesAsync();
            return fixture;
        }
        catch { await fixture.DisposeAsync(); throw; }
    }

    public async Task SeedPaidBookingAsync()
    {
        await using var context = Context();
        context.Bookings.Add(new Bookings { BookingId = 1, RoomId = 1, CustomerId = 10,
            DepositAmount = 700, Status = "Deposited", ExpiryDate = DateTime.UtcNow.AddDays(7) });
        context.Payments.Add(new Payments { UserId = 10, BookingId = 1, Amount = 700, Status = "Success", PaymentMethod = "MoMo" });
        await context.SaveChangesAsync();
    }

    public async Task SeedContractAndInvoiceAsync()
    {
        await SeedPaidBookingAsync();
        await using var context = Context();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var month = new DateOnly(today.Year, today.Month, 1);
        // The contract was processed by another member of staff, so revocation of Manager 7 is allowed.
        context.Contracts.Add(new Contracts { ContractId = 1, BookingId = 1, RoomId = 1, CustomerId = 10,
            StartDate = month, EndDate = today.AddYears(1), MonthlyRent = 700, DepositAmount = 700, Status = "Active" });
        var invoice = new Invoices { InvoiceId = 1, ContractId = 1, InvoiceMonth = month, DueDate = today.AddDays(7), Status = "Unpaid" };
        invoice.InvoiceDetails.Add(new InvoiceDetails { ItemType = InvoiceItemTypes.Rent, Description = "Rent", Amount = 700 });
        invoice.InvoiceDetails.Add(new InvoiceDetails { ItemType = InvoiceItemTypes.Service, Description = "Service", Amount = 200 });
        invoice.InvoiceDetails.Add(new InvoiceDetails { ItemType = InvoiceItemTypes.PrepaidRentCredit, Description = "Prepaid rent", Amount = -700 });
        context.Invoices.Add(invoice);
        await context.Rooms.Where(row => row.RoomId == 1).ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Status, "Occupied"));
        await context.SaveChangesAsync();
    }

    public async Task AssertBlockedByAsync(AppDbContext holder)
    {
        var pid = ((NpgsqlConnection)holder.Database.GetDbConnection()).ProcessID;
        await using var observer = new NpgsqlConnection(_connection);
        await observer.OpenAsync();
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(8))
        {
            await using var command = new NpgsqlCommand("""
                SELECT EXISTS (SELECT 1 FROM pg_stat_activity
                WHERE datname = current_database() AND wait_event_type = 'Lock'
                  AND @holder = ANY(pg_blocking_pids(pid)))
                """, observer);
            command.Parameters.AddWithValue("holder", pid);
            if (await command.ExecuteScalarAsync() is true) return;
            await Task.Delay(25);
        }
        Assert.Fail("The competing transaction did not appear blocked by the expected holder in pg_blocking_pids.");
    }

    public async ValueTask DisposeAsync()
    {
        // The only DROP target is this fixture's generated database; never postgres or a configured arbitrary name.
        Assert.StartsWith("prn232_codex_scopeb_", _database, StringComparison.Ordinal);
        await using var connection = new NpgsqlConnection(_server);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_database}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static Users User(int id, int roleId) => new() { UserId = id, RoleId = roleId, FullName = "Review " + id,
        Email = id + "@codex-review.invalid", PasswordHash = "isolated-test-only", PhoneNumber = "0000000000", IsActive = true };
}

internal sealed class ScopeBLocalRevokeCompatibility(string replacement) : DbCommandInterceptor
{
    private const string Original = "REVOKE ALL PRIVILEGES ON TABLE \"PropertyManagerAssignments\" FROM PUBLIC, anon, authenticated, service_role;";

    public static async Task<ScopeBLocalRevokeCompatibility> CreateAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT rolname FROM pg_roles WHERE rolname = ANY(@names) ORDER BY rolname", connection);
        command.Parameters.AddWithValue("names", new[] { "anon", "authenticated", "service_role" });
        var roles = new List<string> { "PUBLIC" };
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) roles.Add(reader.GetString(0));
        return new ScopeBLocalRevokeCompatibility("REVOKE ALL PRIVILEGES ON TABLE \"PropertyManagerAssignments\" FROM " + string.Join(", ", roles) + ";");
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        command.CommandText = command.CommandText.Replace(Original, replacement, StringComparison.Ordinal);
        return ValueTask.FromResult(result);
    }
}
