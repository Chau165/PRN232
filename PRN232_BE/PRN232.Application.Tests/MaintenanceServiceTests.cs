using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.Services;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using Xunit;

namespace PRN232.Application.Tests;

public class MaintenanceServiceTests
{
    private static readonly DateOnly Today = new(2026, 10, 8);

    [Fact]
    public async Task CreateAsync_creates_pending_request_for_valid_contract_and_equipment()
    {
        var repository = new FakeMaintenanceRepository
        {
            Contract = ValidContract(start: Today, end: Today)
        };
        repository.EquipmentRooms[5] = 12;
        var service = CreateService(repository);

        var created = await service.CreateAsync(new CreateMaintenanceRequestDto(10, 5, "AC is broken"), 42);

        Assert.Equal("Pending", created.Status);
        Assert.Null(created.AssignedTo);
        Assert.Equal(5, created.EquipmentId);
        Assert.Equal(1, repository.AddCount);
        Assert.Equal((null, "Pending", 42),
            (Assert.Single(repository.Logs).OldStatus, repository.Logs[0].NewStatus, repository.Logs[0].ChangedBy));
        Assert.Collection(repository.GeneratedNotifications,
            notification => Assert.Equal("Có yêu cầu bảo trì mới #1 - Phòng A-12", notification.Title),
            notification => Assert.Equal("Bạn đã gửi yêu cầu sửa chữa #1 cho phòng A-12. Yêu cầu đang chờ duyệt.", notification.Title));
    }

    [Fact]
    public async Task CreateAsync_rejects_missing_contract()
    {
        var service = CreateService(new FakeMaintenanceRepository());

        await Assert.ThrowsAsync<MaintenanceNotFoundException>(() =>
            service.CreateAsync(new CreateMaintenanceRequestDto(10, null, "broken"), 42));
    }

    [Theory]
    [InlineData(2026, 10, 9, 2026, 12, 1)]
    [InlineData(2026, 1, 1, 2026, 10, 7)]
    public async Task CreateAsync_rejects_contract_outside_date_range(
        int startYear, int startMonth, int startDay, int endYear, int endMonth, int endDay)
    {
        var repository = new FakeMaintenanceRepository
        {
            Contract = ValidContract(new DateOnly(startYear, startMonth, startDay), new DateOnly(endYear, endMonth, endDay))
        };

        await Assert.ThrowsAsync<MaintenanceContractException>(() =>
            CreateService(repository).CreateAsync(new CreateMaintenanceRequestDto(10, null, "broken"), 42));
        Assert.Equal(0, repository.AddCount);
    }

    [Fact]
    public async Task CreateAsync_rejects_missing_equipment()
    {
        var repository = new FakeMaintenanceRepository { Contract = ValidContract(Today, Today) };

        await Assert.ThrowsAsync<MaintenanceNotFoundException>(() =>
            CreateService(repository).CreateAsync(new CreateMaintenanceRequestDto(10, 5, "broken"), 42));
    }

    [Fact]
    public async Task CreateAsync_rejects_equipment_in_another_room()
    {
        var repository = new FakeMaintenanceRepository { Contract = ValidContract(Today, Today) };
        repository.EquipmentRooms[5] = 99;

        await Assert.ThrowsAsync<MaintenanceEquipmentException>(() =>
            CreateService(repository).CreateAsync(new CreateMaintenanceRequestDto(10, 5, "broken"), 42));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_rejects_blank_description(string description)
    {
        var repository = new FakeMaintenanceRepository { Contract = ValidContract(Today, Today) };

        await Assert.ThrowsAsync<MaintenanceBusinessRuleException>(() =>
            CreateService(repository).CreateAsync(new CreateMaintenanceRequestDto(10, null, description), 42));
    }

    [Fact]
    public async Task GetAllAsync_forwards_customer_status_and_assignee_filters()
    {
        var repository = new FakeMaintenanceRepository();
        var service = CreateService(repository);

        await service.GetAllAsync(42, RoleNames.Customer, "Pending", 7);

        Assert.Equal((42, "Pending", 7), repository.LastFilters);
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Approved")]
    [InlineData("Completed")]
    public void Maintenance_status_filter_accepts_only_final_statuses(string status)
    {
        Assert.True(MaintenanceRequestStatuses.IsValid(status));
    }

    [Theory]
    [InlineData("Unknown")]
    public void Maintenance_status_filter_rejects_obsolete_or_unknown_statuses(string status)
    {
        Assert.False(MaintenanceRequestStatuses.IsValid(status));
    }

    [Fact]
    public async Task GetAllAsync_rejects_unknown_status_filter()
    {
        await Assert.ThrowsAsync<MaintenanceBusinessRuleException>(() =>
            CreateService(new FakeMaintenanceRepository()).GetAllAsync(42, RoleNames.Admin, "Unexpected", null));
    }

    [Fact]
    public async Task CreateReportAsync_rejects_another_managers_request()
    {
        var repository = ValidRequest("Approved", assignedTo: 8);
        var storage = new FakeImageStorage();

        await Assert.ThrowsAsync<MaintenanceAuthorizationException>(() =>
            new MaintenanceService(repository, new FakeNotificationService(), storage, new FixedTimeProvider(DateTimeOffset.UtcNow))
                .CreateReportAsync(1, ValidReportInput(), 7, RoleNames.Manager));
        Assert.Equal(0, storage.UploadCount);
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Completed")]
    public async Task CreateReportAsync_rejects_invalid_request_state(string status)
    {
        var repository = ValidRequest(status, assignedTo: 7);
        var storage = new FakeImageStorage();

        await Assert.ThrowsAsync<MaintenanceStatusTransitionException>(() =>
            new MaintenanceService(repository, new FakeNotificationService(), storage, new FixedTimeProvider(DateTimeOffset.UtcNow))
                .CreateReportAsync(1, ValidReportInput(), 7, RoleNames.Manager));
        Assert.Equal(0, storage.UploadCount);
    }

    [Fact]
    public async Task CreateReportAsync_requires_description_and_at_least_one_image()
    {
        var repository = ValidRequest("Approved", assignedTo: 7);
        var service = new MaintenanceService(repository, new FakeNotificationService(), new FakeImageStorage(), new FixedTimeProvider(DateTimeOffset.UtcNow));

        await Assert.ThrowsAsync<MaintenanceBusinessRuleException>(() =>
            service.CreateReportAsync(1, new CreateMaintenanceReportDto(" ", ValidReportInput().Images), 7, RoleNames.Manager));
        await Assert.ThrowsAsync<MaintenanceBusinessRuleException>(() =>
            service.CreateReportAsync(1, new CreateMaintenanceReportDto("Done", []), 7, RoleNames.Manager));
    }

    [Fact]
    public async Task CreateReportAsync_rejects_mime_spoof_and_does_not_complete_request()
    {
        var repository = ValidRequest("Approved", assignedTo: 7);
        var storage = new FakeImageStorage();
        var invalidImage = new MaintenanceReportImageInput("payload.jpg", "image/jpeg", [0, 1, 2]);

        await Assert.ThrowsAsync<MaintenanceBusinessRuleException>(() =>
            new MaintenanceService(repository, new FakeNotificationService(), storage, new FixedTimeProvider(DateTimeOffset.UtcNow))
                .CreateReportAsync(1, new CreateMaintenanceReportDto("Done", [invalidImage]), 7, RoleNames.Manager));
        Assert.Equal(0, storage.UploadCount);
        Assert.Equal("Approved", repository.State!.Status);
    }

    private static CreateMaintenanceReportDto ValidReportInput() => new(
        "Repaired and tested.",
        [new MaintenanceReportImageInput("evidence.png", "image/png", Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADUlEQVQIHWP4z8AAAAMBAQDJ/pLvAAAAAElFTkSuQmCC"))]);

    [Fact]
    public void Maintenance_lifecycle_allows_only_assignment_and_completion()
    {
        Assert.True(MaintenanceRequestStatuses.IsAllowedTransition("Pending", "Approved"));
        Assert.True(MaintenanceRequestStatuses.IsAllowedTransition("Approved", "Completed"));
        Assert.False(MaintenanceRequestStatuses.IsAllowedTransition("Pending", "Completed"));
        Assert.False(MaintenanceRequestStatuses.IsAllowedTransition("Approved", "Unknown"));
        Assert.False(MaintenanceRequestStatuses.IsAllowedTransition("Pending", "Completed"));
    }

    [Fact]
    public async Task CreateReportAsync_completes_assigned_request_and_logs_manager_identity()
    {
        var repository = ValidRequest("Approved", assignedTo: 7);
        var storage = new FakeImageStorage();
        var service = new MaintenanceService(repository, new FakeNotificationService(), storage, new FixedTimeProvider(DateTimeOffset.UtcNow));

        var report = await service.CreateReportAsync(1, ValidReportInput(), 7, RoleNames.Manager);

        Assert.Equal(7, report.ReportedBy);
        Assert.Equal("Manager", report.ReportedByName);
        Assert.Single(report.Images);
        Assert.StartsWith("https://storage.test/", report.Images[0].ImageUrl);
        Assert.Equal("Completed", repository.State!.Status);
        Assert.Equal(("Approved", "Completed", 7),
            (Assert.Single(repository.Logs).OldStatus, repository.Logs[0].NewStatus, repository.Logs[0].ChangedBy));
    }

    [Fact]
    public async Task CreateReportAsync_cleans_uploaded_image_when_database_completion_fails()
    {
        var repository = ValidRequest("Approved", assignedTo: 7);
        repository.FailReportCompletion = true;
        var storage = new FakeImageStorage();

        await Assert.ThrowsAsync<MaintenanceStatusTransitionException>(() =>
            new MaintenanceService(repository, new FakeNotificationService(), storage, new FixedTimeProvider(DateTimeOffset.UtcNow))
                .CreateReportAsync(1, ValidReportInput(), 7, RoleNames.Manager));
        Assert.Equal(1, storage.UploadCount);
        Assert.Equal(1, storage.DeleteCount);
        Assert.Equal("Approved", repository.State!.Status);
        Assert.Empty(repository.Logs);
    }

    [Fact]
    public async Task CreateReportAsync_rejects_duplicate_report_before_upload()
    {
        var repository = ValidRequest("Approved", assignedTo: 7);
        repository.SeedReport();
        var storage = new FakeImageStorage();

        await Assert.ThrowsAsync<MaintenanceStatusTransitionException>(() =>
            new MaintenanceService(repository, new FakeNotificationService(), storage, new FixedTimeProvider(DateTimeOffset.UtcNow))
                .CreateReportAsync(1, ValidReportInput(), 7, RoleNames.Manager));
        Assert.Equal(0, storage.UploadCount);
    }

    [Fact]
    public async Task CreateReportAsync_storage_failure_does_not_complete_or_log()
    {
        var repository = ValidRequest("Approved", assignedTo: 7);
        var storage = new FakeImageStorage { FailUpload = true };

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            new MaintenanceService(repository, new FakeNotificationService(), storage, new FixedTimeProvider(DateTimeOffset.UtcNow))
                .CreateReportAsync(1, ValidReportInput(), 7, RoleNames.Manager));
        Assert.Equal("Approved", repository.State!.Status);
        Assert.Empty(repository.Logs);
    }

    [Fact]
    public async Task GetReportAsync_allows_admin_and_assigned_manager_only_before_signing_urls()
    {
        var repository = ValidRequest("Completed", assignedTo: 7);
        repository.SeedReport();
        var storage = new FakeImageStorage();
        var service = new MaintenanceService(repository, new FakeNotificationService(), storage, new FixedTimeProvider(DateTimeOffset.UtcNow));

        var report = await service.GetReportAsync(1, 99, RoleNames.Admin);

        Assert.Single(report!.Images);
        Assert.StartsWith("https://storage.test/", report.Images[0].ImageUrl);
        Assert.Equal(1, storage.SignCount);

        report = await service.GetReportAsync(1, 7, RoleNames.Manager);
        Assert.Single(report!.Images);
        Assert.Equal(2, storage.SignCount);

        await Assert.ThrowsAsync<MaintenanceAuthorizationException>(() =>
            service.GetReportAsync(1, 8, RoleNames.Manager));
        await Assert.ThrowsAsync<MaintenanceAuthorizationException>(() =>
            service.GetReportAsync(1, 42, RoleNames.Customer));
        Assert.Equal(2, storage.SignCount);
        Assert.Equal(2, repository.ReportReadCount);
    }

    [Fact]
    public async Task CreateAsync_rejects_another_customers_contract()
    {
        var repository = new FakeMaintenanceRepository { Contract = ValidContract(Today, Today), CustomerOwnsContract = false };

        await Assert.ThrowsAsync<MaintenanceAuthorizationException>(() =>
            CreateService(repository).CreateAsync(new CreateMaintenanceRequestDto(10, null, "broken"), 99));
        Assert.Equal(0, repository.AddCount);
    }

    [Fact]
    public async Task GetByIdAsync_rejects_customer_access_to_another_customers_request()
    {
        var repository = ValidRequest("Pending", null);
        repository.CustomerOwnsContract = false;

        await Assert.ThrowsAsync<MaintenanceAuthorizationException>(() =>
            CreateService(repository).GetByIdAsync(1, 42, RoleNames.Customer));
    }

    [Fact]
    public async Task GetLogsAsync_rejects_customer_access_to_another_customers_request()
    {
        var repository = ValidRequest("Pending", null);
        repository.CustomerOwnsContract = false;

        await Assert.ThrowsAsync<MaintenanceAuthorizationException>(() =>
            CreateService(repository).GetLogsAsync(1, 42, RoleNames.Customer));
    }

    [Fact]
    public async Task UpdateAsync_rejects_manager_role_for_assignment()
    {
        var repository = ValidRequest("Pending", null);
        await Assert.ThrowsAsync<MaintenanceAuthorizationException>(() =>
            CreateService(repository).UpdateAsync(1, new UpdateMaintenanceRequestDto(7), 7, RoleNames.Manager));
        Assert.Empty(repository.Logs);
    }

    [Fact]
    public async Task UpdateAsync_rejects_customer_role()
    {
        var repository = ValidRequest("Pending", null);

        await Assert.ThrowsAsync<MaintenanceAuthorizationException>(() =>
            CreateService(repository).UpdateAsync(1, new UpdateMaintenanceRequestDto(null), 42, RoleNames.Customer));
        Assert.Empty(repository.Logs);
    }

    [Fact]
    public async Task GetByIdAsync_returns_null_for_missing_request()
    {
        var result = await CreateService(new FakeMaintenanceRepository()).GetByIdAsync(404, 42, RoleNames.Admin);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_returns_projected_detail_for_existing_request()
    {
        var repository = ValidRequest("Pending", assignedTo: null);

        var result = await CreateService(repository).GetByIdAsync(1, 42, RoleNames.Admin);

        Assert.NotNull(result);
        Assert.Equal((1, 10, 42, 12, "A-12", (int?)5),
            (result.RequestId, result.ContractId, result.CustomerId, result.RoomId, result.RoomCode, result.EquipmentId));
    }

    [Fact]
    public async Task GetLogsAsync_returns_ordered_logs_for_existing_request()
    {
        var repository = ValidRequest("Completed", assignedTo: 7);
        repository.Logs.Add(new MaintenanceLogDto(2, 1, "Approved", "Completed", null, null,
            new DateTime(2026, 10, 8, 11, 0, 0, DateTimeKind.Utc)));
        repository.Logs.Add(new MaintenanceLogDto(1, 1, "Pending", "Approved", null, null,
            new DateTime(2026, 10, 8, 11, 0, 0, DateTimeKind.Utc)));

        var logs = await CreateService(repository).GetLogsAsync(1, 42, RoleNames.Admin);

        Assert.Equal(new[] { 1, 2 }, logs!.Select(log => log.LogId));
    }

    [Fact]
    public async Task GetLogsAsync_returns_null_for_missing_request()
    {
        var logs = await CreateService(new FakeMaintenanceRepository()).GetLogsAsync(404, 42, RoleNames.Admin);

        Assert.Null(logs);
    }

    [Fact]
    public async Task UpdateAsync_approves_and_assigns_atomically_with_one_utc_log()
    {
        var repository = ValidRequest("Pending", assignedTo: null);
        repository.Contract = ValidContract(Today, Today);
        repository.EquipmentRooms[5] = 12;
        repository.ActiveUsers[7] = new MaintenanceUserAssignmentDto(true, RoleNames.Manager);
        var updated = await CreateService(repository).UpdateAsync(1, new UpdateMaintenanceRequestDto(7), 99, RoleNames.Admin);

        Assert.Equal("Approved", updated!.Status);
        Assert.Equal(7, updated.AssignedTo);
        Assert.Single(repository.Logs);
        var log = Assert.Single(repository.Logs);
        Assert.Equal(("Pending", "Approved", 99), (log.OldStatus, log.NewStatus, log.ChangedBy));
        Assert.Equal(DateTimeKind.Utc, log.ChangedAt.Kind);
        Assert.Equal(1, repository.AtomicUpdateCount);
        Assert.Null(repository.LastAssignmentNote);
    }

    [Fact]
    public async Task UpdateAsync_trims_assignment_note_and_accepts_legacy_request_without_note()
    {
        var repository = ValidRequest("Pending", assignedTo: null);
        repository.ActiveUsers[7] = new MaintenanceUserAssignmentDto(true, RoleNames.Manager);

        var updated = await CreateService(repository).UpdateAsync(1,
            new UpdateMaintenanceRequestDto(7, "  Prepare parts.  "), 99, RoleNames.Admin);

        Assert.Equal("Prepare parts.", updated!.AssignmentNote);
        Assert.Equal("Prepare parts.", repository.LastAssignmentNote);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  \t  ")]
    public async Task UpdateAsync_normalizes_missing_or_whitespace_assignment_note_to_null(string? note)
    {
        var repository = ValidRequest("Pending", assignedTo: null);
        repository.ActiveUsers[7] = new MaintenanceUserAssignmentDto(true, RoleNames.Manager);

        var updated = await CreateService(repository).UpdateAsync(1,
            new UpdateMaintenanceRequestDto(7, note), 99, RoleNames.Admin);

        Assert.Null(updated!.AssignmentNote);
        Assert.Null(repository.LastAssignmentNote);
    }

    [Fact]
    public async Task UpdateAsync_rejects_assignment_note_over_1000_trimmed_characters()
    {
        var repository = ValidRequest("Pending", assignedTo: null);
        repository.ActiveUsers[7] = new MaintenanceUserAssignmentDto(true, RoleNames.Manager);

        await Assert.ThrowsAsync<MaintenanceBusinessRuleException>(() => CreateService(repository).UpdateAsync(1,
            new UpdateMaintenanceRequestDto(7, $" {new string('x', 1001)} "), 99, RoleNames.Admin));

        Assert.Equal("Pending", repository.State!.Status);
        Assert.Empty(repository.Logs);
        Assert.Equal(0, repository.AtomicUpdateCount);
    }

    [Fact]
    public async Task UpdateAsync_rejects_approval_without_assignee()
    {
        var repository = ValidRequest("Pending", assignedTo: null);

        await Assert.ThrowsAsync<MaintenanceAssigneeException>(() =>
            CreateService(repository).UpdateAsync(1, new UpdateMaintenanceRequestDto(null), 99, RoleNames.Admin));
        Assert.Empty(repository.Logs);
    }

    [Fact]
    public async Task UpdateAsync_rejects_missing_assignee()
    {
        var repository = ValidRequest("Pending", assignedTo: null);

        await Assert.ThrowsAsync<MaintenanceNotFoundException>(() =>
            CreateService(repository).UpdateAsync(1, new UpdateMaintenanceRequestDto(7), 99, RoleNames.Admin));
    }

    [Theory]
    [InlineData(false, "Manager")]
    [InlineData(true, "Customer")]
    [InlineData(true, "Admin")]
    public async Task UpdateAsync_rejects_inactive_assignee(bool isActive, string roleName)
    {
        var repository = ValidRequest("Pending", assignedTo: null);
        repository.ActiveUsers[7] = new MaintenanceUserAssignmentDto(isActive, roleName);

        await Assert.ThrowsAsync<MaintenanceAssigneeException>(() =>
            CreateService(repository).UpdateAsync(1, new UpdateMaintenanceRequestDto(7), 99, RoleNames.Admin));
        Assert.Empty(repository.Logs);
    }

    [Fact]
    public async Task UpdateAsync_rejects_reassignment_after_pending_and_manager_actor()
    {
        var repository = ValidRequest("Approved", assignedTo: 7);
        repository.ActiveUsers[8] = new MaintenanceUserAssignmentDto(true, RoleNames.Manager);

        await Assert.ThrowsAsync<MaintenanceStatusTransitionException>(() =>
            CreateService(repository).UpdateAsync(1, new UpdateMaintenanceRequestDto(8), 99, RoleNames.Admin));
        await Assert.ThrowsAsync<MaintenanceAuthorizationException>(() =>
            CreateService(repository).UpdateAsync(1, new UpdateMaintenanceRequestDto(7), 7, RoleNames.Manager));
        Assert.Empty(repository.Logs);
    }

    [Fact]
    public async Task UpdateAsync_rejects_missing_request()
    {
        var repository = new FakeMaintenanceRepository();

        var updated = await CreateService(repository)
            .UpdateAsync(404, new UpdateMaintenanceRequestDto(7), 99, RoleNames.Admin);

        Assert.Null(updated);
        Assert.Empty(repository.Logs);
    }

    [Fact]
    public async Task UpdateAsync_does_not_log_if_request_changed_concurrently()
    {
        var repository = ValidRequest("Pending", assignedTo: null);
        repository.ActiveUsers[7] = new MaintenanceUserAssignmentDto(true, RoleNames.Manager);
        repository.FailAtomicUpdate = true;

        await Assert.ThrowsAsync<MaintenanceStatusTransitionException>(() =>
            CreateService(repository).UpdateAsync(1, new UpdateMaintenanceRequestDto(7), 99, RoleNames.Admin));
        Assert.Empty(repository.Logs);
    }

    [Fact]
    public async Task UpdateAsync_rejects_non_pending_approval_and_does_not_log()
    {
        var repository = ValidRequest("Completed", assignedTo: 7);

        await Assert.ThrowsAsync<MaintenanceStatusTransitionException>(() =>
            CreateService(repository).UpdateAsync(1, new UpdateMaintenanceRequestDto(7), 99, RoleNames.Admin));
        Assert.Empty(repository.Logs);
    }

    [Theory]
    [InlineData("image/jpeg", "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAgGBgcGBQgHBwcJCQgKDBQNDAsLDBkSEw8UHRofHh0aHBwgJC4nICIsIxwcKDcpLDAxNDQ0Hyc5PTgyPC4zNDL/2wBDAQkJCQwLDBgNDRgyIRwhMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjL/wAARCAABAAEDASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwD5/ooooA//2Q==")]
    [InlineData("image/webp", "UklGRiQAAABXRUJQVlA4IBgAAAAwAQCdASoBAAEAAUAmJaQAA3AA/v02aAA=")]
    public async Task CreateReportAsync_accepts_real_jpeg_and_webp_evidence(string type, string base64)
    {
        var repository = ValidRequest("Approved", 7);
        var input = new CreateMaintenanceReportDto("Repaired", [new MaintenanceReportImageInput("evidence", type, Convert.FromBase64String(base64))]);
        await CreateService(repository).CreateReportAsync(1, input, 7, RoleNames.Manager);
        Assert.Equal("Completed", repository.State!.Status);
    }

    [Fact]
    public async Task UpdateAsync_revalidates_contract_dates_at_approval()
    {
        var repository = ValidRequest("Pending", null);
        repository.Contract = ValidContract(new DateOnly(2026, 1, 1), Today.AddDays(-1));
        repository.ActiveUsers[7] = new MaintenanceUserAssignmentDto(true, RoleNames.Manager);
        await Assert.ThrowsAsync<MaintenanceContractException>(() => CreateService(repository).UpdateAsync(1, new UpdateMaintenanceRequestDto(7), 99, RoleNames.Admin));
        Assert.Empty(repository.Logs);
    }

    [Fact]
    public async Task UpdateAsync_rejects_equipment_room_mismatch_at_approval()
    {
        var repository = ValidRequest("Pending", null);
        repository.EquipmentRooms[5] = 99;
        repository.ActiveUsers[7] = new MaintenanceUserAssignmentDto(true, RoleNames.Manager);
        await Assert.ThrowsAsync<MaintenanceEquipmentException>(() => CreateService(repository).UpdateAsync(1, new UpdateMaintenanceRequestDto(7), 99, RoleNames.Admin));
        Assert.Empty(repository.Logs);
    }

    [Fact]
    public async Task UpdateAsync_rejects_assignee_that_disappeared_before_approval()
    {
        var repository = ValidRequest("Pending", null);
        await Assert.ThrowsAsync<MaintenanceNotFoundException>(() => CreateService(repository).UpdateAsync(1, new UpdateMaintenanceRequestDto(7), 99, RoleNames.Admin));
        Assert.Empty(repository.Logs);
    }

    [Theory]
    [InlineData("Pending", "Approved")]
    [InlineData("Approved", "Completed")]
    public async Task UpdateAsync_accepts_allowed_transitions(string oldStatus, string newStatus)
    {
        var repository = ValidRequest(oldStatus, 7);
        repository.ActiveUsers[7] = new MaintenanceUserAssignmentDto(true, RoleNames.Manager);
        var service = CreateService(repository);
        if (newStatus == "Approved")
        {
            await service.UpdateAsync(1, new UpdateMaintenanceRequestDto(7), 99, RoleNames.Admin);
        }
        else
        {
            await service.CreateReportAsync(1, ValidReportInput(), 7, RoleNames.Manager);
        }

        Assert.Equal(newStatus, repository.State!.Status);
        Assert.Single(repository.Logs);
    }

    [Theory]
    [InlineData("Approved", "Completed")]
    [InlineData("Completed", "Pending")]
    [InlineData("Rejected", "InProgress")]
    [InlineData("Completed", "Approved")]
    public async Task UpdateAsync_rejects_disallowed_transitions(string oldStatus, string attemptedStatus)
    {
        var repository = ValidRequest(oldStatus, 7);
        // PATCH no longer accepts any status, including this attempted status.
        Assert.Null(typeof(UpdateMaintenanceRequestDto).GetProperty("Status"));
        Assert.NotEqual(oldStatus, attemptedStatus);
        await Assert.ThrowsAsync<MaintenanceStatusTransitionException>(() => CreateService(repository).UpdateAsync(1, new UpdateMaintenanceRequestDto(7), 99, RoleNames.Admin));
        Assert.Empty(repository.Logs);
        Assert.Equal(oldStatus, repository.State!.Status);
    }

    [Fact]
    public async Task UpdateAsync_preserves_assignment_when_later_transition_omits_it()
    {
        var repository = ValidRequest("Approved", 7);
        await CreateService(repository).CreateReportAsync(1, ValidReportInput(), 7, RoleNames.Manager);
        Assert.Equal(7, repository.State!.AssignedTo);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UpdateAsync_validates_explicit_assignment_on_later_transition(bool active)
    {
        var repository = ValidRequest("Approved", 7);
        repository.ActiveUsers[8] = new MaintenanceUserAssignmentDto(active, RoleNames.Manager);
        await Assert.ThrowsAsync<MaintenanceStatusTransitionException>(() => CreateService(repository).UpdateAsync(1, new UpdateMaintenanceRequestDto(8), 99, RoleNames.Admin));
        Assert.Equal(7, repository.State!.AssignedTo);
        Assert.Empty(repository.Logs);
    }

    [Fact]
    public async Task UpdateAsync_rejects_missing_explicit_assignment_on_later_transition()
    {
        var repository = ValidRequest("Approved", 7);
        await Assert.ThrowsAsync<MaintenanceStatusTransitionException>(() => CreateService(repository).UpdateAsync(1, new UpdateMaintenanceRequestDto(8), 99, RoleNames.Admin));
        Assert.Equal(7, repository.State!.AssignedTo);
        Assert.Empty(repository.Logs);
    }

    [Fact]
    public async Task UpdateAsync_rejects_inactive_explicit_assignment_on_later_transition()
    {
        var repository = ValidRequest("Approved", 7);
        repository.ActiveUsers[8] = new MaintenanceUserAssignmentDto(false, RoleNames.Manager);
        await Assert.ThrowsAsync<MaintenanceStatusTransitionException>(() => CreateService(repository).UpdateAsync(1, new UpdateMaintenanceRequestDto(8), 99, RoleNames.Admin));
        Assert.Equal(7, repository.State!.AssignedTo);
        Assert.Empty(repository.Logs);
    }

    private static MaintenanceService CreateService(FakeMaintenanceRepository repository) =>
        new(repository, new FakeNotificationService(), new FakeImageStorage(), new FixedTimeProvider(new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero)));

    private static FakeMaintenanceRepository ValidRequest(string status, int? assignedTo) => new()
    {
        State = new MaintenanceRequestStateDto(1, 10, 5, assignedTo, status),
        Contract = ValidContract(Today, Today),
        EquipmentRooms = { [5] = 12 }
    };

    private static MaintenanceContractDto ValidContract(DateOnly start, DateOnly end) =>
        new(10, 12, "A-12", 42, start, end, "Unfinalized status");

    private sealed class FakeNotificationService : INotificationService
    {
        public Task<List<NotificationDto>> GetAllAsync(int userId, bool? isRead, string? type) =>
            Task.FromResult(new List<NotificationDto>());

        public Task UpdateReadStateAsync(int id, int userId, bool? isRead) => Task.CompletedTask;

        public Task<List<int>> GetActiveAdminUserIdsAsync() => Task.FromResult(new List<int> { 99 });

        public IReadOnlyList<Notifications> BuildMaintenanceNotifications(IEnumerable<int> userIds, string title) =>
            userIds.Distinct().Select(userId => new Notifications
            {
                UserId = userId,
                Title = title,
                Type = NotificationTypes.Maintenance
            }).ToList();
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class FakeMaintenanceRepository : IMaintenanceRepository
    {
        private int _nextId = 1;

        public MaintenanceContractDto? Contract
        {
            get; set;
        }
        public MaintenanceRequestStateDto? State
        {
            get; set;
        }
        public Dictionary<int, int> EquipmentRooms { get; } = new();
        public Dictionary<int, MaintenanceUserAssignmentDto> ActiveUsers { get; } = new();
        public List<MaintenanceLogDto> Logs { get; } = new();
        public List<Notifications> GeneratedNotifications { get; } = new();
        public int AddCount
        {
            get; private set;
        }
        public int AtomicUpdateCount
        {
            get; private set;
        }
        public string? LastAssignmentNote
        {
            get; private set;
        }
        public bool FailAtomicUpdate
        {
            get; set;
        }
        public bool FailReportCompletion
        {
            get; set;
        }
        public MaintenanceReports? SavedReport
        {
            get; private set;
        }
        public bool RequestExists { get; set; } = true;
        public bool CustomerOwnsContract { get; set; } = true;
        public int ReportReadCount
        {
            get; private set;
        }
        public (int? CustomerId, string? Status, int? AssignedTo) LastFilters
        {
            get; private set;
        }

        public Task<List<MaintenanceRequestReadModel>> GetAllAsync(int? customerId, int? assignedManagerId, string? status, int? assignedTo)
        {
            LastFilters = (customerId, status, assignedTo);
            return Task.FromResult(new List<MaintenanceRequestReadModel>());
        }

        public Task<MaintenanceRequestReadModel?> GetByIdAsync(int id, int? customerId, int? assignedManagerId) =>
            Task.FromResult(State?.RequestId == id && (!customerId.HasValue || CustomerOwnsContract) &&
                            (!assignedManagerId.HasValue || assignedManagerId == State?.AssignedTo) ? ToReadModel(ToDto(State)) : null);

        public Task<MaintenanceRequestStateReadModel?> GetStateByIdAsync(int id, int? assignedManagerId) =>
            Task.FromResult(State?.RequestId == id && (!assignedManagerId.HasValue || assignedManagerId == State?.AssignedTo)
                ? new MaintenanceRequestStateReadModel(State.RequestId, State.ContractId, State.EquipmentId, State.AssignedTo, State.Status)
                : null);

        public Task<bool> RequestExistsAsync(int id) => Task.FromResult(RequestExists);

        public Task<MaintenanceContractReadModel?> GetContractAsync(int contractId) =>
            Task.FromResult(Contract?.ContractId == contractId && Contract is not null
                ? new MaintenanceContractReadModel(Contract.ContractId, Contract.RoomId, Contract.RoomCode, Contract.CustomerId,
                    Contract.StartDate, Contract.EndDate, Contract.Status)
                : null);

        public Task<int?> GetEquipmentRoomIdAsync(int equipmentId) =>
            Task.FromResult(EquipmentRooms.TryGetValue(equipmentId, out var roomId) ? (int?)roomId : null);

        public Task<MaintenanceUserAssignmentReadModel?> GetUserAssignmentAsync(int userId) =>
            Task.FromResult(ActiveUsers.TryGetValue(userId, out var assignment)
                ? new MaintenanceUserAssignmentReadModel(assignment.IsActive, assignment.RoleName)
                : null);

        public Task<MaintenanceRequests> AddWithInitialLogAsync(
            MaintenanceRequests request,
            MaintenanceLogs log,
            Func<int, IReadOnlyCollection<Notifications>> notificationsFactory)
        {
            request.RequestId = _nextId++;
            State = new MaintenanceRequestStateDto(request.RequestId, request.ContractId, request.EquipmentId,
                request.AssignedTo, request.Status);
            AddCount++;
            Logs.Add(new MaintenanceLogDto(Logs.Count + 1, request.RequestId, log.OldStatus, log.NewStatus,
                log.ChangedBy, null, log.ChangedAt));
            GeneratedNotifications.AddRange(notificationsFactory(request.RequestId));
            return Task.FromResult(request);
        }

        public Task<bool> UpdateStatusAndAddLogAsync(int requestId, string expectedStatus, string status,
            int? assignedTo, string? assignmentNote, int? changedBy, DateTime changedAt,
            IReadOnlyCollection<Notifications>? notifications = null)
        {
            if (FailAtomicUpdate)
            {
                return Task.FromResult(false);
            }
            if (State?.RequestId != requestId)
            {
                return Task.FromResult(false);
            }
            if (State.Status != expectedStatus)
            {
                return Task.FromResult(false);
            }

            Logs.Add(new MaintenanceLogDto(Logs.Count + 1, requestId, State.Status, status, changedBy,
                null, changedAt));
            LastAssignmentNote = assignmentNote;
            _assignmentNote = assignmentNote;
            State = State with
            {
                Status = status,
                AssignedTo = assignedTo ?? State.AssignedTo
            };
            AtomicUpdateCount++;
            return Task.FromResult(true);
        }

        public Task<List<MaintenanceLogReadModel>?> GetLogsAsync(int requestId, int? customerId, int? assignedManagerId) =>
            Task.FromResult(State?.RequestId == requestId && (!customerId.HasValue || CustomerOwnsContract) &&
                            (!assignedManagerId.HasValue || assignedManagerId == State?.AssignedTo)
                ? Logs.OrderBy(x => x.ChangedAt).ThenBy(x => x.LogId)
                    .Select(log => new MaintenanceLogReadModel(log.LogId, log.RequestId, log.OldStatus, log.NewStatus,
                        log.ChangedBy, log.ChangedByName, log.ChangedAt)).ToList()
                : null);

        public Task<int?> GetApprovingAdminIdAsync(int requestId) => Task.FromResult<int?>(9);

        public Task<MaintenanceReportReadModel?> GetReportAsync(int requestId)
        {
            ReportReadCount++;
            return Task.FromResult<MaintenanceReportReadModel?>(SavedReport is null || SavedReport.RequestId != requestId
                ? null
                : new MaintenanceReportReadModel(SavedReport.ReportId, SavedReport.RequestId, SavedReport.Description,
                    SavedReport.ReportedBy, "Manager", SavedReport.CreatedAt,
                    SavedReport.Images.Select((image, index) => new MaintenanceReportImageReadModel(index + 1, image.ImageUrl)).ToList()));
        }
        public Task<bool> ReportExistsAsync(int requestId) => Task.FromResult(SavedReport?.RequestId == requestId);
        public Task<bool> CompleteWithReportAsync(int requestId, MaintenanceReports report, int changedBy, DateTime changedAt, IReadOnlyCollection<Notifications>? notifications = null)
        {
            if (FailReportCompletion || State?.RequestId != requestId || State.Status != "Approved")
            {
                return Task.FromResult(false);
            }

            report.ReportId = 1;
            report.RequestId = requestId;
            SavedReport = report;
            Logs.Add(new MaintenanceLogDto(Logs.Count + 1, requestId, "Approved", "Completed", changedBy, "Manager", changedAt));
            State = State with
            {
                Status = "Completed"
            };
            return Task.FromResult(true);
        }

        public void SeedReport() => SavedReport = new MaintenanceReports
        {
            ReportId = 1,
            RequestId = 1,
            ReportedBy = 7,
            Description = "Done",
            CreatedAt = DateTime.UtcNow,
            Images = [new MaintenanceReportImages { ImageUrl = "maintenance/1/evidence.png" }]
        };

        private string? _assignmentNote;

        private MaintenanceRequestDto ToDto(MaintenanceRequestStateDto state) => new(
            state.RequestId, "broken", state.Status, state.ContractId, 42, "Customer", 12, "A-12",
            state.EquipmentId, state.EquipmentId.HasValue ? "AC" : null,
            state.EquipmentId.HasValue ? "Working" : null, state.AssignedTo,
            state.AssignedTo.HasValue ? "Staff" : null, _assignmentNote);

        private static MaintenanceRequestReadModel ToReadModel(MaintenanceRequestDto request) => new(
            request.RequestId, request.Description, request.Status, request.ContractId, request.CustomerId,
            request.CustomerName, request.RoomId, request.RoomCode, request.EquipmentId, request.EquipmentName,
            request.EquipmentStatus, request.AssignedTo, request.AssignedStaffName, request.AssignmentNote);
    }

    private sealed class FakeImageStorage : IMaintenanceReportImageStorage
    {
        public int UploadCount
        {
            get; private set;
        }
        public int DeleteCount
        {
            get; private set;
        }
        public int SignCount
        {
            get; private set;
        }
        public bool FailUpload
        {
            get; set;
        }
        public Task<string> UploadAsync(int requestId, MaintenanceReportImageInput image)
        {
            UploadCount++;
            if (FailUpload)
            {
                throw new HttpRequestException("Storage unavailable");
            }

            return Task.FromResult($"{requestId}/{image.FileName}");
        }
        public Task<string> GetReadUrlAsync(string objectPath)
        {
            SignCount++;
            return Task.FromResult($"https://storage.test/{objectPath}");
        }
        public Task DeleteAsync(string objectPath)
        {
            DeleteCount++;
            return Task.CompletedTask;
        }
    }
}
