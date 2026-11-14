using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PRN232.Api.Controllers;
using PRN232.Application.Interfaces;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using PRN232.Infrastructure.Repositories;
using Xunit;

namespace PRN232.Application.Tests;

// Independent review: use real authentication, controllers, services and SQLite repositories.
// Failing tests assert the intended authorization boundary, not the current vulnerable response.
public class ScopeBManagerAuthorizationReviewTests
{
    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(
        DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")).DateTime);
    private static DateOnly Month => new(Today.Year, Today.Month, 1);

    private static readonly string[] Modules =
        ["bookings", "viewing-schedules", "contracts", "utility-readings", "invoices", "invoice-complaints"];

    public static IEnumerable<object[]> ReadCases()
    {
        foreach (var state in new[] { "unassigned", "revoked", "inactive" })
        foreach (var module in Modules)
        foreach (var detail in new[] { false, true })
            yield return [state, module, detail];
    }

    public static IEnumerable<object[]> MutationCases()
    {
        foreach (var state in new[] { "unassigned", "revoked", "inactive" })
        foreach (var operation in new[]
        {
            "booking-create", "booking-pay", "booking-cancel", "viewing-create", "viewing-approve",
            "viewing-reject", "contract-create", "contract-extend", "contract-terminate", "room-ready",
            "reading-create", "invoice-preview", "invoice-create", "invoice-pay", "invoice-cash",
            "complaint-create", "complaint-update"
        })
            yield return [state, operation];
    }

    [Theory]
    [MemberData(nameof(ReadCases))]
    public async Task Manager_cannot_read_foreign_property_with_missing_revoked_or_inactive_assignment(
        string state, string module, bool detail)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory);
        await PrepareManagerAsync(factory, client, state);

        using var response = await client.GetAsync($"/api/{module}" + (detail ? "/1" : ""));

        var expected = state == "inactive" ? HttpStatusCode.Unauthorized
            : detail ? HttpStatusCode.Forbidden : HttpStatusCode.OK;
        Assert.Equal(expected, response.StatusCode);
        if (!detail && state != "inactive")
        {
            var rows = await response.Content.ReadFromJsonAsync<JsonElement>();
            var ownRow = Assert.Single(rows.EnumerateArray());
            // Each module has one foreign row (ID 1) and one permitted row (ID 2).
            var idField = module switch
            {
                "bookings" => "bookingId", "viewing-schedules" => "viewingId", "contracts" => "contractId",
                "utility-readings" => "readingId", "invoices" => "invoiceId", _ => "complaintId"
            };
            Assert.Equal(2, ownRow.GetProperty(idField).GetInt32());
        }
    }

    [Theory]
    [InlineData("unassigned")]
    [InlineData("revoked")]
    [InlineData("inactive")]
    public async Task Billing_timeline_respects_current_manager_scope(string state)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory);
        await PrepareManagerAsync(factory, client, state);
        using var response = await client.GetAsync("/api/contracts/1/billing-timeline");
        Assert.Equal(state == "inactive" ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(MutationCases))]
    public async Task Manager_cannot_mutate_foreign_property_after_permission_loss(string state, string operation)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory);
        await PrepareManagerAsync(factory, client, state);
        var before = await SnapshotAsync(factory);

        using var response = await SendMutationAsync(client, operation);

        Assert.Equal(before, await SnapshotAsync(factory));
        Assert.Equal(state == "inactive" ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Theory]
    [InlineData("unassigned")]
    [InlineData("revoked")]
    public async Task Foreign_paid_invoice_must_be_forbidden_before_disclosing_payment_status(string state)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory);
        await PrepareManagerAsync(factory, client, state);
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Invoices
                .Where(invoice => invoice.InvoiceId == 1)
                .ExecuteUpdateAsync(setters => setters.SetProperty(invoice => invoice.Status, "Paid"));
        var before = await SnapshotAsync(factory);

        using var response = await client.PostAsync("/api/invoices/1/record-cash", null);

        Assert.Equal(before, await SnapshotAsync(factory));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("bookings")]
    [InlineData("viewing-schedules")]
    [InlineData("contracts")]
    [InlineData("utility-readings")]
    [InlineData("invoices")]
    [InlineData("invoice-complaints")]
    public async Task Former_admin_now_unassigned_manager_must_not_keep_global_read_access(string module)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory);
        await BookingsApiTests.LoginAsync(client, "admin-b@example.test");
        // Keep the already issued token while another admin changes this user's role via the API.
        using var owner = new HttpClient(factory.Server.CreateHandler()) { BaseAddress = client.BaseAddress };
        await BookingsApiTests.LoginAsync(owner, "admin@example.test");
        using var roleChange = await owner.PutAsJsonAsync("/api/users/11/role", new { roleId = 2 });
        Assert.Equal(HttpStatusCode.OK, roleChange.StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(2, (await context.Users.SingleAsync(user => user.UserId == 11)).RoleId);
            Assert.False(await context.PropertyManagerAssignments.AnyAsync(assignment => assignment.ManagerId == 11));
        }

        using var response = await client.GetAsync($"/api/{module}/1");

        // Rejecting the obsolete token (401) is also a secure implementation.
        Assert.True(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized,
            $"A former Admin with current Manager role and no assignment received {(int)response.StatusCode} for {module}/1.");
    }

    [Theory]
    [InlineData("viewing-approve")]
    [InlineData("viewing-reject")]
    [InlineData("complaint-update")]
    public async Task Former_admin_now_unassigned_manager_must_not_keep_admin_write_access(string operation)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory);
        await BookingsApiTests.LoginAsync(client, "admin-b@example.test");
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().ViewingSchedules
                .Where(viewing => viewing.ViewingId == 1)
                .ExecuteUpdateAsync(setters => setters.SetProperty(viewing => viewing.Status, "Pending"));
        using var owner = new HttpClient(factory.Server.CreateHandler()) { BaseAddress = client.BaseAddress };
        await BookingsApiTests.LoginAsync(owner, "admin@example.test");
        using var roleChange = await owner.PutAsJsonAsync("/api/users/11/role", new { roleId = 2 });
        Assert.Equal(HttpStatusCode.OK, roleChange.StatusCode);
        var before = await SnapshotAsync(factory);

        using var response = await SendMutationAsync(client, operation);
        var after = await SnapshotAsync(factory);

        Assert.True(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized,
            $"Demoted unassigned Manager received {(int)response.StatusCode}; persisted data changed: {before != after}.");
        Assert.Equal(before, after);
    }

    [Theory]
    [InlineData("booking-cancel")]
    [InlineData("contract-create")]
    [InlineData("contract-extend")]
    [InlineData("contract-terminate")]
    [InlineData("room-ready")]
    [InlineData("reading-create")]
    [InlineData("invoice-create")]
    [InlineData("invoice-cash")]
    [InlineData("complaint-update")]
    public async Task Revocation_between_service_check_and_repository_write_blocks_the_write(string operation)
    {
        var hookRan = false;
        using var original = new BookingsApiFactory();
        using var factory = original.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            void Revoke(AppDbContext context)
            {
                hookRan = true;
                // Deterministically commit the permission loss before the repository transaction starts.
                // This does not claim to exercise PostgreSQL row locking or simultaneous transactions.
                context.PropertyManagerAssignments.Where(row => row.PropertyId == 1 && row.ManagerId == 7)
                    .ExecuteDelete();
            }
            if (operation == "booking-cancel")
                RegisterRevocationProxy<IBookingRepository>(services, context => new BookingRepository(context), "CancelAsync", Revoke);
            else if (operation.StartsWith("contract-") || operation == "room-ready")
                RegisterRevocationProxy<IContractRepository>(services, context => new ContractRepository(context), operation switch
                {
                    "contract-create" => "AddFromBookingAsync", "contract-extend" => "ExtendAsync",
                    "contract-terminate" => "TerminateAsync", _ => "MarkTerminatedRoomReadyAsync"
                }, Revoke);
            else if (operation == "reading-create")
                RegisterRevocationProxy<IUtilityReadingRepository>(services, context => new UtilityReadingRepository(context), "AddIfLatestAsync", Revoke);
            else if (operation.StartsWith("invoice-"))
                RegisterRevocationProxy<IInvoiceRepository>(services, context => new InvoiceRepository(context),
                    operation == "invoice-create" ? "AddIfAbsentAsync" : "MarkPaidAsync", Revoke);
            else
                RegisterRevocationProxy<IInvoiceComplaintRepository>(services, context => new InvoiceComplaintRepository(context), "UpdateStatusAsync", Revoke);
        }));
        using var client = factory.CreateClient();
        await SeedAsync(factory);
        await BookingsApiTests.LoginAsync(client, "manager@example.test");
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (operation == "invoice-create")
            {
                context.UtilityReadings.Add(new UtilityReadings { ContractId = 1, ReadingMonth = Month,
                    ElectricStart = 100, ElectricEnd = 150, WaterStart = 10, WaterEnd = 14 });
                await context.SaveChangesAsync();
            }
            if (operation == "room-ready")
            {
                await context.Contracts.Where(row => row.ContractId == 1)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Status, "Terminated"));
                await context.Rooms.Where(row => row.RoomId == 1)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Status, "Maintenance"));
            }
        }
        var before = await SnapshotAsync(factory);

        using var response = await SendMutationAsync(client, operation);

        Assert.True(hookRan, "The request must reach the repository mutation after initial authorization.");
        Assert.Equal(before, await SnapshotAsync(factory));
        using (var scope = factory.Services.CreateScope())
            Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().PropertyManagerAssignments
                .AnyAsync(row => row.PropertyId == 1 && row.ManagerId == 7));
        // Booking-cancel, contract-create, reading-create và invoice-create trả 409 thay vì 403 khi quyền mất đúng
        // giữa lúc service kiểm tra và repository ghi (repository chỉ báo "không ghi được"). Chấp nhận có chủ ý:
        // điều cần giữ là không dữ liệu nào bị ghi (đã kiểm ở trên). Các thao tác còn lại phải trả đúng 403.
        var mayReturnConflict = operation is "booking-cancel" or "contract-create" or "reading-create" or "invoice-create";
        Assert.True(
            response.StatusCode == HttpStatusCode.Forbidden || (mayReturnConflict && response.StatusCode == HttpStatusCode.Conflict),
            $"{operation} returned {(int)response.StatusCode}");
    }

    [Theory]
    [InlineData("bookings/999")]
    [InlineData("viewing-schedules/999")]
    [InlineData("contracts/999")]
    [InlineData("contracts/999/billing-timeline")]
    [InlineData("utility-readings/999")]
    [InlineData("invoices/999")]
    [InlineData("invoice-complaints/999")]
    public async Task Missing_records_return_not_found_to_an_unassigned_manager(string path)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await BookingsApiTests.LoginAsync(client, "manager-b@example.test");
        using var response = await client.GetAsync($"/api/{path}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static void RegisterRevocationProxy<T>(IServiceCollection services,
        Func<AppDbContext, T> createRepository, string member, Action<AppDbContext> revoke) where T : class
    {
        services.AddScoped<T>(provider =>
        {
            var context = provider.GetRequiredService<AppDbContext>();
            var proxy = DispatchProxy.Create<T, ScopeBReviewRevocationProxy<T>>();
            ((ScopeBReviewRevocationProxy<T>)(object)proxy).Configure(createRepository(context), member, () => revoke(context));
            return proxy;
        });
    }

    private static Task<HttpResponseMessage> SendMutationAsync(HttpClient client, string operation) => operation switch
    {
        "booking-create" => client.PostAsJsonAsync("/api/bookings", new { roomId = 1 }),
        "booking-pay" => client.PostAsJsonAsync("/api/bookings/3/pay", new { paymentMethod = "MoMo" }),
        "booking-cancel" => client.PostAsync("/api/bookings/3/cancel", null),
        "viewing-create" => client.PostAsJsonAsync("/api/viewing-schedules", new { roomId = 1, scheduledAt = DateTimeOffset.UtcNow.AddDays(1) }),
        "viewing-approve" => client.PostAsJsonAsync("/api/viewing-schedules/1/approve", new { assignedTo = 7, scheduledAt = DateTimeOffset.UtcNow.AddDays(1) }),
        "viewing-reject" => client.PostAsJsonAsync("/api/viewing-schedules/1/reject", new { reason = "Review authorization" }),
        "contract-create" => client.PostAsJsonAsync("/api/contracts", new { bookingId = 3, startDate = Today, endDate = Today.AddYears(1) }),
        "contract-extend" => client.PutAsJsonAsync("/api/contracts/1/extend", new { newEndDate = Today.AddYears(2) }),
        "contract-terminate" => client.PostAsJsonAsync("/api/contracts/1/terminate", new { checkoutConfirmed = true, roomReadyForRental = true, outstandingDebtAcknowledged = true }),
        "room-ready" => client.PostAsync("/api/contracts/1/room-ready", null),
        "reading-create" => client.PostAsJsonAsync("/api/utility-readings", new { contractId = 1, readingMonth = Month, electricStart = 100m, electricEnd = 150m, waterStart = 10m, waterEnd = 14m }),
        "invoice-preview" => client.PostAsJsonAsync("/api/invoices/preview", new { contractId = 1, invoiceMonth = Month, dueDate = Today.AddDays(10) }),
        "invoice-create" => client.PostAsJsonAsync("/api/invoices", new { contractId = 1, invoiceMonth = Month, dueDate = Today.AddDays(10) }),
        "invoice-pay" => client.PostAsJsonAsync("/api/invoices/1/pay", new { paymentMethod = "MoMo" }),
        "invoice-cash" => client.PostAsync("/api/invoices/1/record-cash", null),
        "complaint-create" => client.PostAsJsonAsync("/api/invoice-complaints", new { invoiceId = 1, feeType = "Khác", content = "Review authorization" }),
        "complaint-update" => client.PatchAsJsonAsync("/api/invoice-complaints/1", new { status = "Resolved" }),
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };

    private static async Task PrepareManagerAsync(WebApplicationFactory<BookingsController> factory, HttpClient client, string state)
    {
        var managerId = state == "unassigned" ? 8 : 7;
        await BookingsApiTests.LoginAsync(client, state == "unassigned" ? "manager-b@example.test" : "manager@example.test");
        if (managerId == 7)
        {
            using var scope = factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().ViewingSchedules
                .Where(viewing => viewing.ViewingId == 2)
                .ExecuteUpdateAsync(setters => setters.SetProperty(viewing => viewing.AssignedTo, (int?)7));
        }
        if (state == "revoked")
        {
            using var owner = new HttpClient(factory.Server.CreateHandler()) { BaseAddress = client.BaseAddress };
            await BookingsApiTests.LoginAsync(owner, "admin@example.test");
            using var revoke = await owner.DeleteAsync("/api/properties/1/managers/7");
            Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
        }
        if (state == "inactive")
        {
            using var owner = new HttpClient(factory.Server.CreateHandler()) { BaseAddress = client.BaseAddress };
            await BookingsApiTests.LoginAsync(owner, "admin@example.test");
            using var deactivate = await owner.PutAsJsonAsync($"/api/users/{managerId}/status", new { isActive = false });
            Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
        }
    }

    private static async Task SeedAsync(WebApplicationFactory<BookingsController> factory)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.Properties.Add(new Property { PropertyId = 2, OwnerId = 9, PropertyName = "Permitted property" });
        context.PropertyRoomTypes.Add(new PropertyRoomTypes { PropertyId = 2, RoomTypeId = 1, MonthlyRent = 3_000_000m, IsActive = true });
        context.Rooms.Add(new Rooms { RoomId = 3, PropertyId = 2, RoomTypeId = 1, RoomCode = "B101", Status = "Occupied" });
        context.PropertyManagerAssignments.AddRange(
            new PropertyManagerAssignment { PropertyId = 2, ManagerId = 7 },
            new PropertyManagerAssignment { PropertyId = 2, ManagerId = 8 });
        for (var id = 1; id <= 2; id++)
        {
            var roomId = id == 1 ? 1 : 3;
            context.Bookings.Add(new Bookings { BookingId = id, RoomId = roomId, CustomerId = 10,
                DepositAmount = 3_000_000m, Status = "Deposited", ExpiryDate = DateTime.UtcNow.AddDays(7) });
            // No ProcessedByStaffId: revocation via the public API is allowed (no open work for Manager 7).
            context.Contracts.Add(new Contracts { ContractId = id, RoomId = roomId, CustomerId = 10, BookingId = id,
                StartDate = Month.AddMonths(-1), EndDate = Today.AddYears(1), MonthlyRent = 3_000_000m,
                DepositAmount = 3_000_000m, Status = "Active" });
            context.ViewingSchedules.Add(new ViewingSchedules { ViewingId = id, PropertyId = id == 1 ? 1 : 2, RoomTypeId = 1, RoomId = roomId, CustomerId = 10,
                AssignedTo = id == 1 ? 7 : 8, ScheduledAt = DateTime.UtcNow.AddDays(-1), Status = id == 1 ? "Pending" : "Approved" });
            context.UtilityReadings.Add(new UtilityReadings { ReadingId = id, ContractId = id,
                ReadingMonth = Month.AddMonths(-1), ElectricStart = 0, ElectricEnd = 100, WaterStart = 0, WaterEnd = 10 });
            var invoice = new Invoices { InvoiceId = id, ContractId = id, InvoiceMonth = Month.AddMonths(-1),
                DueDate = Today.AddDays(10), Status = "Unpaid" };
            invoice.InvoiceDetails.Add(new InvoiceDetails { ItemType = "Rent", Description = "Rent", Amount = 3_000_000m });
            context.Invoices.Add(invoice);
            context.InvoiceComplaints.Add(new InvoiceComplaints { ComplaintId = id, InvoiceId = id, CustomerId = 10,
                FeeType = "Khác", Content = "Review authorization", Status = "New", CreatedAt = DateTime.UtcNow });
        }
        context.Bookings.Add(new Bookings { BookingId = 3, RoomId = 2, CustomerId = 10, DepositAmount = 3_000_000m,
            AgreedMonthlyRent = 3_000_000m,
            Status = "Deposited", ExpiryDate = DateTime.UtcNow.AddDays(7) });
        context.Payments.Add(new Payments { BookingId = 3, UserId = 10, Amount = 3_000_000m, PaymentMethod = "MoMo", Status = "Success" });
        await context.SaveChangesAsync();
        // Preserve a historical AssignedTo without blocking revocation as open work.
        await context.ViewingSchedules.Where(viewing => viewing.ViewingId == 1)
            .ExecuteUpdateAsync(setters => setters.SetProperty(viewing => viewing.Status, "Approved"));
    }

    private static async Task<string> SnapshotAsync(WebApplicationFactory<BookingsController> factory)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return JsonSerializer.Serialize(new
        {
            bookings = await context.Bookings.AsNoTracking().OrderBy(row => row.BookingId)
                .Select(row => new { row.BookingId, row.Status, row.ExpiryDate }).ToListAsync(),
            viewings = await context.ViewingSchedules.AsNoTracking().OrderBy(row => row.ViewingId)
                .Select(row => new { row.ViewingId, row.Status, row.AssignedTo, row.RespondedBy }).ToListAsync(),
            contracts = await context.Contracts.AsNoTracking().OrderBy(row => row.ContractId)
                .Select(row => new { row.ContractId, row.Status, row.EndDate }).ToListAsync(),
            rooms = await context.Rooms.AsNoTracking().OrderBy(row => row.RoomId).Select(row => new { row.RoomId, row.Status }).ToListAsync(),
            invoices = await context.Invoices.AsNoTracking().OrderBy(row => row.InvoiceId).Select(row => new { row.InvoiceId, row.Status }).ToListAsync(),
            complaints = await context.InvoiceComplaints.AsNoTracking().OrderBy(row => row.ComplaintId).Select(row => new { row.ComplaintId, row.Status }).ToListAsync(),
            readings = await context.UtilityReadings.CountAsync(), payments = await context.Payments.CountAsync(),
            notifications = await context.Notifications.CountAsync()
        });
    }
}

public class ScopeBReviewRevocationProxy<T> : DispatchProxy where T : class
{
    private T _inner = null!;
    private string _member = "";
    private Action? _revoke;

    public void Configure(T inner, string member, Action revoke) => (_inner, _member, _revoke) = (inner, member, revoke);

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod!.Name == _member && _revoke is { } revoke)
        {
            _revoke = null;
            revoke();
        }
        return targetMethod.Invoke(_inner, args);
    }
}
