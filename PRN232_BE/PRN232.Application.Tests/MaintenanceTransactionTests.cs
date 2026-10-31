using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PRN232.Application.DTOs;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using PRN232.Infrastructure.Repositories;
using Xunit;

namespace PRN232.Application.Tests;

public class MaintenanceTransactionTests
{
    [Fact]
    public async Task Creation_rolls_back_request_when_initial_log_cannot_be_saved()
    {
        using var factory = new MaintenanceApiFactory();
        using var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await RejectInsertAsync(context, "MaintenanceLogs");
            await Assert.ThrowsAsync<DbUpdateException>(() => new MaintenanceRepository(context).AddWithInitialLogAsync(
                new MaintenanceRequests { ContractId = 1, Description = "Broken", Status = "Pending" },
                new MaintenanceLogs { NewStatus = "Pending", ChangedBy = 10, ChangedAt = DateTime.UtcNow },
                _ => []));
        }
        using var verification = factory.Services.CreateScope();
        var stored = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(4, await stored.MaintenanceRequests.CountAsync());
        Assert.Empty(await stored.MaintenanceLogs.ToListAsync());
    }

    [Fact]
    public async Task Creation_commits_customer_confirmation_and_admin_notifications_with_request_and_log()
    {
        using var factory = new MaintenanceApiFactory();
        using var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await new MaintenanceRepository(context).AddWithInitialLogAsync(
                new MaintenanceRequests { ContractId = 1, Description = "Broken", Status = "Pending" },
                new MaintenanceLogs { NewStatus = "Pending", ChangedBy = 10, ChangedAt = DateTime.UtcNow },
                requestId =>
                [
                    new Notifications { UserId = 9, Title = $"Admin request #{requestId}", Type = "Maintenance", IsRead = false },
                    new Notifications { UserId = 10, Title = $"Customer confirmation #{requestId}", Type = "Maintenance", IsRead = false }
                ]);
        }

        using var verification = factory.Services.CreateScope();
        var stored = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(5, await stored.MaintenanceRequests.CountAsync());
        Assert.Equal(1, await stored.MaintenanceLogs.CountAsync(log => log.RequestId == 5));
        Assert.Equal(new[] { 9, 10 }, await stored.Notifications.OrderBy(notification => notification.UserId)
            .Select(notification => notification.UserId).ToArrayAsync());
        Assert.All(await stored.Notifications.ToArrayAsync(), notification => Assert.False(notification.IsRead));
    }

    [Fact]
    public async Task Creation_rolls_back_request_and_log_when_notification_cannot_be_saved()
    {
        using var factory = new MaintenanceApiFactory();
        using var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await RejectInsertAsync(context, "Notifications");
            await Assert.ThrowsAsync<DbUpdateException>(() => new MaintenanceRepository(context).AddWithInitialLogAsync(
                new MaintenanceRequests { ContractId = 1, Description = "Broken", Status = "Pending" },
                new MaintenanceLogs { NewStatus = "Pending", ChangedBy = 10, ChangedAt = DateTime.UtcNow },
                _ =>
                [
                    new Notifications { UserId = 9, Title = "new request for admin", Type = "Maintenance" },
                    new Notifications { UserId = 10, Title = "request confirmation for customer", Type = "Maintenance" }
                ]));
        }
        using var verification = factory.Services.CreateScope();
        var stored = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(4, await stored.MaintenanceRequests.CountAsync());
        Assert.Empty(await stored.MaintenanceLogs.ToListAsync());
        Assert.Empty(await stored.Notifications.ToListAsync());
    }

    [Fact]
    public async Task Assignment_rolls_back_status_and_assignee_when_log_cannot_be_saved()
    {
        using var factory = new MaintenanceApiFactory();
        using var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await RejectInsertAsync(context, "MaintenanceLogs");
            await Assert.ThrowsAsync<DbUpdateException>(() => new MaintenanceRepository(context)
                .UpdateStatusAndAddLogAsync(3, "Pending", "Approved", 7, "rollback note", 9, DateTime.UtcNow));
        }
        using var verification = factory.Services.CreateScope();
        var stored = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        var request = await stored.MaintenanceRequests.SingleAsync(r => r.RequestId == 3);
        Assert.Equal("Pending", request.Status);
        Assert.Null(request.AssignedTo);
        Assert.Null(request.AssignmentNote);
        Assert.Empty(await stored.MaintenanceLogs.ToListAsync());
    }

    [Fact]
    public async Task Assignment_status_guard_allows_only_one_approval_note_log_and_notification_set()
    {
        using var factory = new MaintenanceApiFactory();
        using var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var repository = new MaintenanceRepository(context);
            Assert.True(await repository.UpdateStatusAndAddLogAsync(3, "Pending", "Approved", 7,
                "first admin note", 9, DateTime.UtcNow,
                [new Notifications { UserId = 10, Title = "approved", Type = "Maintenance" }]));
            Assert.False(await repository.UpdateStatusAndAddLogAsync(3, "Pending", "Approved", 8,
                "stale admin note", 12, DateTime.UtcNow,
                [new Notifications { UserId = 10, Title = "duplicate approval", Type = "Maintenance" }]));
        }

        using var verification = factory.Services.CreateScope();
        var stored = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        var request = (await stored.MaintenanceRequests.FindAsync(3))!;
        Assert.Equal(("Approved", 7, "first admin note"),
            (request.Status, request.AssignedTo, request.AssignmentNote));
        Assert.Single(await stored.MaintenanceLogs.ToListAsync());
        Assert.Single(await stored.Notifications.ToListAsync());
    }

    [Fact]
    public async Task Assignment_rolls_back_status_and_log_when_notification_cannot_be_saved()
    {
        using var factory = new MaintenanceApiFactory();
        using var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await RejectInsertAsync(context, "Notifications");
            await Assert.ThrowsAsync<DbUpdateException>(() => new MaintenanceRepository(context)
                .UpdateStatusAndAddLogAsync(3, "Pending", "Approved", 7, "rollback note", 9, DateTime.UtcNow,
                    [new Notifications { UserId = 10, Title = "approved", Type = "Maintenance" }]));
        }
        using var verification = factory.Services.CreateScope();
        var stored = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        var request = await stored.MaintenanceRequests.FindAsync(3);
        Assert.Equal("Pending", request!.Status);
        Assert.Null(request.AssignedTo);
        Assert.Null(request.AssignmentNote);
        Assert.Empty(await stored.MaintenanceLogs.ToListAsync());
        Assert.Empty(await stored.Notifications.ToListAsync());
    }

    [Fact]
    public async Task Completion_rolls_back_status_report_images_and_log_on_database_failure()
    {
        using var factory = new MaintenanceApiFactory();
        using var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await RejectInsertAsync(context, "MaintenanceReportImages");
            await Assert.ThrowsAsync<DbUpdateException>(() => new MaintenanceRepository(context)
                .CompleteWithReportAsync(1, Report(), 7, DateTime.UtcNow));
        }
        using var verification = factory.Services.CreateScope();
        var stored = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("Approved", (await stored.MaintenanceRequests.FindAsync(1))!.Status);
        Assert.Empty(await stored.MaintenanceReports.Where(r => r.RequestId == 1).ToListAsync());
        Assert.Equal(1, await stored.MaintenanceReportImages.CountAsync());
        Assert.Empty(await stored.MaintenanceLogs.ToListAsync());
    }

    [Fact]
    public async Task Completion_rolls_back_report_and_log_when_notification_cannot_be_saved()
    {
        using var factory = new MaintenanceApiFactory();
        using var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await RejectInsertAsync(context, "Notifications");
            await Assert.ThrowsAsync<DbUpdateException>(() => new MaintenanceRepository(context)
                .CompleteWithReportAsync(1, Report(), 7, DateTime.UtcNow,
                    [new Notifications { UserId = 10, Title = "completed", Type = "Maintenance" }]));
        }
        using var verification = factory.Services.CreateScope();
        var stored = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("Approved", (await stored.MaintenanceRequests.FindAsync(1))!.Status);
        Assert.Empty(await stored.MaintenanceReports.Where(report => report.RequestId == 1).ToListAsync());
        Assert.Empty(await stored.MaintenanceLogs.ToListAsync());
        Assert.Empty(await stored.Notifications.ToListAsync());
    }

    [Fact]
    public async Task Completion_rechecks_manager_assignment_at_atomic_persistence()
    {
        using var factory = new MaintenanceApiFactory();
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await new MaintenanceRepository(context).CompleteWithReportAsync(2, Report(), 7, DateTime.UtcNow));
        context.ChangeTracker.Clear();
        Assert.Equal("Approved", (await context.MaintenanceRequests.FindAsync(2))!.Status);
        Assert.Empty(await context.MaintenanceReports.Where(r => r.RequestId == 2).ToListAsync());
        Assert.Empty(await context.MaintenanceLogs.ToListAsync());
    }

    [Fact]
    public async Task Request_can_have_only_one_report_in_relational_database()
    {
        using var factory = new MaintenanceApiFactory();
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var report = Report();
        report.RequestId = 4;
        context.MaintenanceReports.Add(report);
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Uploaded_objects_are_cleaned_up_when_database_persistence_throws()
    {
        using var factory = new MaintenanceApiFactory();
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await RejectInsertAsync(context, "MaintenanceReportImages");
        var service = scope.ServiceProvider.GetRequiredService<PRN232.Application.Interfaces.IMaintenanceService>();
        await Assert.ThrowsAsync<DbUpdateException>(() => service.CreateReportAsync(1,
            new CreateMaintenanceReportDto("Repaired", [new MaintenanceReportImageInput("evidence.png", "image/png", MaintenanceFlowApiTests.Png)]), 7, "Manager"));
        Assert.Equal(1, factory.Storage.UploadCount);
        Assert.Equal(1, factory.Storage.DeleteCount);
    }

    private static MaintenanceReports Report() => new()
    {
        ReportedBy = 7,
        Description = "Repaired",
        CreatedAt = DateTime.UtcNow,
        Images = [new MaintenanceReportImages { ImageUrl = "maintenance/test/evidence.png" }]
    };

    private static Task RejectInsertAsync(AppDbContext context, string table)
    {
        var sql = table switch
        {
            "MaintenanceLogs" => "CREATE TRIGGER reject_insert BEFORE INSERT ON \"MaintenanceLogs\" BEGIN SELECT RAISE(ABORT, 'test injected failure'); END",
            "MaintenanceReportImages" => "CREATE TRIGGER reject_insert BEFORE INSERT ON \"MaintenanceReportImages\" BEGIN SELECT RAISE(ABORT, 'test injected failure'); END",
            "Notifications" => "CREATE TRIGGER reject_insert BEFORE INSERT ON \"Notifications\" BEGIN SELECT RAISE(ABORT, 'test injected failure'); END",
            _ => throw new ArgumentException("Unsupported fault injection table.", nameof(table))
        };
        return context.Database.ExecuteSqlRawAsync(sql);
    }
}
