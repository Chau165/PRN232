using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PRN232.Api.Controllers;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using Xunit;

namespace PRN232.Application.Tests;

public class MaintenanceFlowApiTests
{
    internal const string Password = "test-password-123";
    internal static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADUlEQVQIHWP4z8AAAAMBAQDJ/pLvAAAAAElFTkSuQmCC");

    [Theory]
    [InlineData("GET", "/api/maintenance-requests/1/reports")]
    [InlineData("POST", "/api/maintenance-requests")]
    [InlineData("PATCH", "/api/maintenance-requests/1")]
    [InlineData("POST", "/api/maintenance-requests/1/reports")]
    public async Task Anonymous_writes_and_reports_require_authentication(string method, string path)
    {
        using var factory = new MaintenanceApiFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task Target_register_login_and_customer_create_preserve_auth_contract()
    {
        using var factory = new MaintenanceApiFactory();
        using var client = factory.CreateClient();
        var registration = await client.PostAsJsonAsync("/api/auth/register", new
        { fullName = "New Customer", email = "  NEW@example.test ", password = Password, phoneNumber = "123", roleId = 3, roleName = "Admin", isActive = false, passwordHash = "spoofed" });
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        var registered = await registration.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Customer", registered.GetProperty("user").GetProperty("roleName").GetString());
        var userId = registered.GetProperty("user").GetProperty("userId").GetInt32();
        Assert.DoesNotContain("passwordHash", registered.ToString(), StringComparison.OrdinalIgnoreCase);
        await LoginAsync(client, "new@example.test");
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await context.Users.SingleAsync(u => u.UserId == userId);
            Assert.True(user.IsActive);
            Assert.True(BCrypt.Net.BCrypt.Verify(Password, user.PasswordHash));
            Assert.Equal(1, user.RoleId);
            context.Contracts.Add(new Contracts { ContractId = 100, CustomerId = userId, RoomId = 1, StartDate = new DateOnly(2020, 1, 1), EndDate = new DateOnly(2100, 1, 1) });
            await context.SaveChangesAsync();
        }
        var response = await client.PostAsJsonAsync("/api/maintenance-requests", new
        { contractId = 100, description = "Leaking faucet", status = "Completed", assignedTo = 8, changedBy = 999, customerId = 20 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Pending", body.GetProperty("status").GetString());
        Assert.Equal(userId, body.GetProperty("customerId").GetInt32());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("assignedTo").ValueKind);
        using var finalScope = factory.Services.CreateScope();
        var log = await finalScope.ServiceProvider.GetRequiredService<AppDbContext>().MaintenanceLogs.SingleAsync();
        Assert.Equal((null, "Pending", userId), (log.OldStatus, log.NewStatus, log.ChangedBy));
    }

    [Fact]
    public async Task Target_auth_rejects_duplicate_invalid_login_and_inactive_account()
    {
        using var factory = new MaintenanceApiFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/auth/register", new
        { fullName = "Duplicate", email = "customer@example.test", password = Password, phoneNumber = "123" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { email = "customer@example.test", password = "wrong" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/auth/login", new { email = "inactive@example.test", password = Password })).StatusCode);
    }

    [Theory]
    [InlineData("customer@example.test", "1,3,4")]
    [InlineData("other@example.test", "2")]
    [InlineData("manager@example.test", "1,4")]
    [InlineData("manager-b@example.test", "2")]
    [InlineData("admin@example.test", "1,2,3,4")]
    public async Task Lists_use_customer_or_assigned_manager_scope(string email, string expected)
    {
        using var factory = new MaintenanceApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, email);
        var results = await client.GetFromJsonAsync<JsonElement>("/api/maintenance-requests");
        Assert.Equal(expected, string.Join(',', results.EnumerateArray().Select(r => r.GetProperty("requestId").GetInt32())));
    }

    [Theory]
    [InlineData("customer@example.test")]
    [InlineData("manager@example.test")]
    public async Task Filters_cannot_override_ownership(string email)
    {
        using var factory = new MaintenanceApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, email);
        var results = await client.GetFromJsonAsync<JsonElement>("/api/maintenance-requests?customerId=20&assignedTo=8&status=Approved");
        Assert.Empty(results.EnumerateArray());
    }

    [Theory]
    [InlineData("customer@example.test", "/api/maintenance-requests/2")]
    [InlineData("customer@example.test", "/api/maintenance-requests/2/logs")]
    [InlineData("manager@example.test", "/api/maintenance-requests/2")]
    [InlineData("manager@example.test", "/api/maintenance-requests/2/logs")]
    public async Task Detail_and_logs_deny_unrelated_users(string email, string path)
    {
        using var factory = new MaintenanceApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, email);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Customer_cannot_create_for_another_contract()
    {
        using var factory = new MaintenanceApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/maintenance-requests", new { contractId = 2, description = "Spoof", customerId = 20 })).StatusCode);
    }

    [Theory]
    [InlineData(10, HttpStatusCode.BadRequest)]
    [InlineData(9, HttpStatusCode.BadRequest)]
    [InlineData(11, HttpStatusCode.BadRequest)]
    [InlineData(999, HttpStatusCode.NotFound)]
    public async Task Assignment_requires_existing_active_manager(int assignedTo, HttpStatusCode expected)
    {
        using var factory = new MaintenanceApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "admin@example.test");
        Assert.Equal(expected, (await client.PatchAsJsonAsync("/api/maintenance-requests/3", new { assignedTo })).StatusCode);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("Pending", (await context.MaintenanceRequests.FindAsync(3))!.Status);
        Assert.Empty(await context.MaintenanceLogs.ToListAsync());
    }

    [Fact]
    public async Task Admin_cannot_complete_by_patch()
    {
        using var factory = new MaintenanceApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "admin@example.test");
        Assert.Equal(HttpStatusCode.Conflict, (await client.PatchAsJsonAsync("/api/maintenance-requests/1", new { status = "Completed", assignedTo = 7, changedBy = 999 })).StatusCode);
    }

    [Theory]
    [InlineData("customer@example.test", "PATCH", "/api/maintenance-requests/3")]
    [InlineData("manager@example.test", "PATCH", "/api/maintenance-requests/3")]
    [InlineData("admin@example.test", "POST", "/api/maintenance-requests/1/reports")]
    [InlineData("customer@example.test", "POST", "/api/maintenance-requests/1/reports")]
    [InlineData("manager@example.test", "GET", "/api/maintenance-requests/4/reports")]
    [InlineData("customer@example.test", "GET", "/api/maintenance-requests/4/reports")]
    public async Task Role_authorization_uses_target_jwt(string email, string method, string path)
    {
        using var factory = new MaintenanceApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, email);
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Content = method == "POST" ? ReportForm() : JsonContent.Create(new { assignedTo = 7 });
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(request)).StatusCode);
    }

    [Theory]
    [InlineData(2, HttpStatusCode.Forbidden)]
    [InlineData(3, HttpStatusCode.Conflict)]
    [InlineData(4, HttpStatusCode.Conflict)]
    [InlineData(999, HttpStatusCode.NotFound)]
    public async Task Completion_requires_ownership_approved_state_and_existing_request(int id, HttpStatusCode expected)
    {
        using var factory = new MaintenanceApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "manager@example.test");
        if (id == 3)
        {
            using var scope = factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await context.MaintenanceRequests.FindAsync(3))!.AssignedTo = 7;
            await context.SaveChangesAsync();
        }
        using var form = ReportForm();
        Assert.Equal(expected, (await client.PostAsync($"/api/maintenance-requests/{id}/reports", form)).StatusCode);
        Assert.Equal(0, factory.Storage.UploadCount);
    }

    [Theory]
    [InlineData(0, "image/png", false, false, "Done")]
    [InlineData(6, "image/png", false, false, "Done")]
    [InlineData(1, "text/plain", false, false, "Done")]
    [InlineData(1, "image/png", true, false, "Done")]
    [InlineData(1, "image/png", false, true, "Done")]
    [InlineData(1, "image/png", false, false, "")]
    [InlineData(1, "image/png", false, false, "   ")]
    public async Task Report_validates_description_count_type_data_and_size(int count, string type, bool oversized, bool spoofed, string description)
    {
        using var factory = new MaintenanceApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "manager@example.test");
        using var form = ReportForm(description, count, type, oversized ? new byte[5 * 1024 * 1024 + 1] : spoofed ? [1, 2, 3] : Png);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/maintenance-requests/1/reports", form)).StatusCode);
        Assert.Equal(0, factory.Storage.UploadCount);
        using var scope = factory.Services.CreateScope();
        Assert.Equal("Approved", (await scope.ServiceProvider.GetRequiredService<AppDbContext>().MaintenanceRequests.FindAsync(1))!.Status);
    }

    [Fact]
    public async Task Full_lifecycle_uses_three_jwt_identities_and_private_report_urls()
    {
        using var factory = new MaintenanceApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");
        var created = await client.PostAsJsonAsync("/api/maintenance-requests", new { contractId = 1, description = "Leaking faucet" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("requestId").GetInt32();
        await LoginAsync(client, "admin@example.test");
        var assigned = await client.PatchAsJsonAsync($"/api/maintenance-requests/{id}", new { assignedTo = 7, status = "Completed", changedBy = 999 });
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
        var assignment = await assigned.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Approved", assignment.GetProperty("status").GetString());
        Assert.Equal(7, assignment.GetProperty("assignedTo").GetInt32());
        await LoginAsync(client, "manager@example.test");
        using var form = ReportForm();
        foreach (var name in new[] { "ReportedBy", "ChangedBy", "ManagerId" }) form.Add(new StringContent("999"), name);
        form.Add(new StringContent("Pending"), "Status");
        var completion = await client.PostAsync($"/api/maintenance-requests/{id}/reports", form);
        Assert.Equal(HttpStatusCode.Created, completion.StatusCode);
        var report = await completion.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(7, report.GetProperty("reportedBy").GetInt32());
        Assert.Equal(id, report.GetProperty("requestId").GetInt32());
        using var duplicate = ReportForm();
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/maintenance-requests/{id}/reports", duplicate)).StatusCode);
        Assert.Equal(1, factory.Storage.UploadCount);
        await LoginAsync(client, "admin@example.test");
        var reviewed = await client.GetFromJsonAsync<JsonElement>($"/api/maintenance-requests/{id}/reports");
        Assert.Equal("Repaired and tested", reviewed.GetProperty("description").GetString());
        Assert.Equal("Manager A", reviewed.GetProperty("reportedByName").GetString());
        Assert.Equal(7, reviewed.GetProperty("reportedBy").GetInt32());
        Assert.True(reviewed.GetProperty("createdAt").GetDateTime() > DateTime.UtcNow.AddMinutes(-5));
        Assert.StartsWith("https://storage.test/", reviewed.GetProperty("images")[0].GetProperty("imageUrl").GetString());
        var logs = await client.GetFromJsonAsync<JsonElement>($"/api/maintenance-requests/{id}/logs");
        Assert.Equal(new[] { "Pending", "Approved", "Completed" }, logs.EnumerateArray().Select(l => l.GetProperty("newStatus").GetString()));
        Assert.Equal(new[] { 10, 9, 7 }, logs.EnumerateArray().Select(l => l.GetProperty("changedBy").GetInt32()));
        Assert.Equal(JsonValueKind.Null, logs[0].GetProperty("oldStatus").ValueKind);
        Assert.Equal("Pending", logs[1].GetProperty("oldStatus").GetString());
        Assert.Equal("Approved", logs[2].GetProperty("oldStatus").GetString());
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("Completed", (await context.MaintenanceRequests.FindAsync(id))!.Status);
        var stored = await context.MaintenanceReports.Include(r => r.Images).SingleAsync(r => r.RequestId == id);
        Assert.StartsWith($"maintenance/{id}/", Assert.Single(stored.Images).ImageUrl);
        Assert.DoesNotContain("https://", stored.Images.Single().ImageUrl);
    }

    [Fact]
    public async Task Storage_failure_returns_502_without_completing_or_logging()
    {
        using var factory = new MaintenanceApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "manager@example.test");
        factory.Storage.FailUpload = true;
        using var form = ReportForm();
        Assert.Equal(HttpStatusCode.BadGateway, (await client.PostAsync("/api/maintenance-requests/1/reports", form)).StatusCode);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("Approved", (await context.MaintenanceRequests.FindAsync(1))!.Status);
        Assert.Empty(await context.MaintenanceReports.Where(r => r.RequestId == 1).ToListAsync());
        Assert.Empty(await context.MaintenanceLogs.ToListAsync());
    }

    [Fact]
    public async Task Admin_report_missing_is_404_and_swagger_exposes_exact_contracts()
    {
        using var factory = new MaintenanceApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "admin@example.test");
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/maintenance-requests/1/reports")).StatusCode);
        using var swagger = JsonDocument.Parse(await client.GetStringAsync("/swagger/v1/swagger.json"));
        var tagNames = swagger.RootElement.GetProperty("tags").EnumerateArray()
            .Select(tag => tag.GetProperty("name").GetString()).ToArray();
        Assert.Equal(new[] { "HealthChecks", "Auth", "Users" }, tagNames.Take(3));
        Assert.Equal(tagNames.Skip(3).OrderBy(name => name, StringComparer.OrdinalIgnoreCase), tagNames.Skip(3));
        var paths = swagger.RootElement.GetProperty("paths");
        foreach (var (path, verbs) in new[] { ("/api/maintenance-requests", new[] { "get", "post" }), ("/api/maintenance-requests/{id}", new[] { "get", "patch" }), ("/api/maintenance-requests/{id}/logs", new[] { "get" }), ("/api/maintenance-requests/{id}/reports", new[] { "get", "post" }) })
            foreach (var verb in verbs) Assert.True(paths.GetProperty(path).TryGetProperty(verb, out _));
        Assert.True(paths.TryGetProperty("/api/auth/login", out _));
        Assert.True(paths.TryGetProperty("/api/auth/register", out _));
        Assert.False(swagger.RootElement.TryGetProperty("security", out var globalSecurity) && globalSecurity.GetArrayLength() > 0);
        var bearerScheme = swagger.RootElement.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        Assert.Equal("http", bearerScheme.GetProperty("type").GetString());
        Assert.Equal("bearer", bearerScheme.GetProperty("scheme").GetString());
        Assert.Equal("JWT", bearerScheme.GetProperty("bearerFormat").GetString());
        Assert.True(paths.GetProperty("/api/maintenance-requests").GetProperty("get").GetProperty("security")[0].TryGetProperty("Bearer", out _));
        Assert.False(paths.GetProperty("/api/auth/login").GetProperty("post").TryGetProperty("security", out var loginSecurity) && loginSecurity.GetArrayLength() > 0);
        var properties = paths.GetProperty("/api/maintenance-requests/{id}/reports").GetProperty("post").GetProperty("requestBody").GetProperty("content").GetProperty("multipart/form-data").GetProperty("schema").GetProperty("properties");
        Assert.Equal(new[] { "Description", "Images" }, properties.EnumerateObject().Select(p => p.Name));
        var schemas = swagger.RootElement.GetProperty("components").GetProperty("schemas");
        Assert.Equal(new[] { "assignedTo" }, schemas.GetProperty("UpdateMaintenanceRequestDto").GetProperty("properties").EnumerateObject().Select(p => p.Name));
        Assert.False(schemas.GetProperty("CreateMaintenanceRequestDto").GetProperty("properties").TryGetProperty("customerId", out _));
    }

    [Fact]
    public async Task Existing_target_profile_and_admin_user_endpoints_still_use_same_jwt()
    {
        using var factory = new MaintenanceApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");
        var profile = await client.GetFromJsonAsync<JsonElement>("/api/users/me");
        Assert.Equal(10, profile.GetProperty("userId").GetInt32());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/users")).StatusCode);
        await LoginAsync(client, "admin@example.test");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/users")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-jwt-token");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/maintenance-requests")).StatusCode);
    }

    [Theory]
    [InlineData("/api/maintenance-requests?status=InProgress", HttpStatusCode.BadRequest)]
    [InlineData("/api/maintenance-requests/999", HttpStatusCode.NotFound)]
    [InlineData("/api/maintenance-requests/999/logs", HttpStatusCode.NotFound)]
    public async Task Read_errors_use_expected_http_semantics(string path, HttpStatusCode expected)
    {
        using var factory = new MaintenanceApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "admin@example.test");
        Assert.Equal(expected, (await client.GetAsync(path)).StatusCode);
    }

    [Theory]
    [InlineData(999, "Broken", HttpStatusCode.NotFound)]
    [InlineData(1, "", HttpStatusCode.BadRequest)]
    [InlineData(1, "   ", HttpStatusCode.BadRequest)]
    public async Task Customer_creation_errors_do_not_persist_requests_or_logs(int contractId, string description, HttpStatusCode expected)
    {
        using var factory = new MaintenanceApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "customer@example.test");
        Assert.Equal(expected, (await client.PostAsJsonAsync("/api/maintenance-requests", new { contractId, description })).StatusCode);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(4, await context.MaintenanceRequests.CountAsync());
        Assert.Empty(await context.MaintenanceLogs.ToListAsync());
    }

    [Theory]
    [InlineData(3, HttpStatusCode.BadRequest)]
    [InlineData(999, HttpStatusCode.NotFound)]
    public async Task Assignment_missing_manager_or_request_returns_expected_error(int id, HttpStatusCode expected)
    {
        using var factory = new MaintenanceApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "admin@example.test");
        Assert.Equal(expected, (await client.PatchAsJsonAsync($"/api/maintenance-requests/{id}", new { assignedTo = (int?)null })).StatusCode);
    }

    [Fact]
    public async Task Signing_failure_preserves_committed_report_and_returns_502()
    {
        using var factory = new MaintenanceApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "manager@example.test");
        factory.Storage.FailSigning = true;
        using var form = ReportForm();
        Assert.Equal(HttpStatusCode.BadGateway, (await client.PostAsync("/api/maintenance-requests/1/reports", form)).StatusCode);
        Assert.Equal(0, factory.Storage.DeleteCount);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal("Completed", (await context.MaintenanceRequests.FindAsync(1))!.Status);
            Assert.Single(await context.MaintenanceReports.Where(r => r.RequestId == 1).ToListAsync());
        }
        await LoginAsync(client, "admin@example.test");
        Assert.Equal(HttpStatusCode.BadGateway, (await client.GetAsync("/api/maintenance-requests/1/reports")).StatusCode);
        factory.Storage.FailSigning = false;
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/maintenance-requests/1/reports")).StatusCode);
    }

    private static async Task LoginAsync(HttpClient client, string email)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
    }

    private static MultipartFormDataContent ReportForm(string description = "Repaired and tested", int count = 1, string type = "image/png", byte[]? bytes = null)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(description), "Description");
        for (var index = 0; index < count; index++)
        {
            var file = new ByteArrayContent(bytes ?? Png);
            file.Headers.ContentType = new MediaTypeHeaderValue(type);
            form.Add(file, "Images", $"evidence-{index}.png");
        }
        return form;
    }
}

internal sealed class MaintenanceApiFactory : WebApplicationFactory<MaintenanceRequestsController>
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    public TestReportImageStorage Storage { get; } = new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "integration-test-only-signing-key-32-bytes", ["Jwt:Issuer"] = "test-issuer",
            ["Jwt:Audience"] = "test-audience", ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=unused"
        }));
        builder.ConfigureTestServices(services =>
        {
            _connection.Open();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IMaintenanceReportImageStorage>();
            services.AddSingleton<IMaintenanceReportImageStorage>(Storage);
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
        });
    }
    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.Database.EnsureCreated();
        var hash = BCrypt.Net.BCrypt.HashPassword(MaintenanceFlowApiTests.Password, workFactor: 4);
        context.Roles.AddRange(new Roles { RoleId = 1, RoleName = "Customer" }, new Roles { RoleId = 2, RoleName = "Manager" }, new Roles { RoleId = 3, RoleName = "Admin" });
        context.Users.AddRange(User(10, 1, "Customer A", "customer@example.test", hash), User(20, 1, "Customer B", "other@example.test", hash), User(7, 2, "Manager A", "manager@example.test", hash), User(8, 2, "Manager B", "manager-b@example.test", hash), User(9, 3, "Admin", "admin@example.test", hash), User(11, 2, "Inactive", "inactive@example.test", hash, false));
        context.Properties.Add(new Property { PropertyId = 1, OwnerId = 7 });
        context.RoomTypes.Add(new RoomTypes { RoomTypeId = 1, TypeName = "Studio" });
        context.Rooms.AddRange(new Rooms { RoomId = 1, PropertyId = 1, RoomTypeId = 1, RoomCode = "A-1" }, new Rooms { RoomId = 2, PropertyId = 1, RoomTypeId = 1, RoomCode = "A-2" });
        context.Contracts.AddRange(Contract(1, 10, 1), Contract(2, 20, 2));
        context.MaintenanceRequests.AddRange(Request(1, 1, 7, "Approved"), Request(2, 2, 8, "Approved"), Request(3, 1, null, "Pending"), Request(4, 1, 7, "Completed"));
        context.MaintenanceReports.Add(new MaintenanceReports { ReportId = 1, RequestId = 4, ReportedBy = 7, Description = "Existing report", CreatedAt = DateTime.UtcNow, Images = [new MaintenanceReportImages { ImageUrl = "maintenance/4/existing.png" }] });
        context.SaveChanges();
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
    private static Users User(int id, int roleId, string name, string email, string hash, bool active = true) => new() { UserId = id, RoleId = roleId, FullName = name, Email = email, PasswordHash = hash, IsActive = active };
    private static Contracts Contract(int id, int customerId, int roomId) => new() { ContractId = id, CustomerId = customerId, RoomId = roomId, StartDate = new DateOnly(2020, 1, 1), EndDate = new DateOnly(2100, 1, 1) };
    private static MaintenanceRequests Request(int id, int contractId, int? managerId, string status) => new() { RequestId = id, ContractId = contractId, AssignedTo = managerId, Description = "Broken fixture", Status = status };
}

internal sealed class TestReportImageStorage : IMaintenanceReportImageStorage
{
    public int UploadCount { get; private set; }
    public int DeleteCount { get; private set; }
    public bool FailUpload { get; set; }
    public bool FailSigning { get; set; }
    public Task<string> UploadAsync(int requestId, MaintenanceReportImageInput image)
    {
        UploadCount++;
        if (FailUpload) throw new HttpRequestException("Test storage failure");
        return Task.FromResult($"maintenance/{requestId}/{Guid.NewGuid():N}.png");
    }
    public Task<string> GetReadUrlAsync(string path)
    {
        if (FailSigning) throw new HttpRequestException("Test signing failure");
        return Task.FromResult($"https://storage.test/{path}?signed=test");
    }
    public Task DeleteAsync(string path)
    {
        DeleteCount++;
        return Task.CompletedTask;
    }
}
