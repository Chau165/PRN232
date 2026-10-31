using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.Services;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using Xunit;

namespace PRN232.Application.Tests;

public class InvoiceComplaintServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly October = new(2026, 10, 1);
    private const string RentLine = "Tiền phòng tháng 10/2026";

    [Fact]
    public async Task CreateAsync_files_a_new_complaint_about_a_real_invoice_line_and_notifies_the_owner()
    {
        var repository = new FakeComplaintRepository();

        await CreateService(repository).CreateAsync(new CreateInvoiceComplaintDto(1, $"  {RentLine} ", "  Sai giá phòng  "), 10);

        var (complaint, notification) = Assert.Single(repository.Added);
        Assert.Equal((1, 10, RentLine, "Sai giá phòng", ComplaintStatuses.New),
            (complaint.InvoiceId, complaint.CustomerId, complaint.FeeType, complaint.Content, complaint.Status));
        Assert.Equal(Now.UtcDateTime, complaint.CreatedAt);
        Assert.Equal((7, NotificationTypes.Complaint), (notification.UserId, notification.Type));
        Assert.Contains("10/2026", notification.Title);
    }

    [Fact]
    public async Task CreateAsync_accepts_a_general_complaint()
    {
        var repository = new FakeComplaintRepository();

        await CreateService(repository).CreateAsync(new CreateInvoiceComplaintDto(1, ComplaintStatuses.OtherFeeType, "Hạn thanh toán quá gấp"), 10);

        Assert.Equal(ComplaintStatuses.OtherFeeType, Assert.Single(repository.Added).Complaint.FeeType);
    }

    [Theory]
    [InlineData("Tiền gửi xe", "Sai")]
    [InlineData("", "Sai")]
    [InlineData(RentLine, "   ")]
    public async Task CreateAsync_rejects_fee_types_not_on_the_invoice_and_empty_content(string feeType, string content)
    {
        var repository = new FakeComplaintRepository();

        await Assert.ThrowsAsync<ComplaintBusinessRuleException>(() =>
            CreateService(repository).CreateAsync(new CreateInvoiceComplaintDto(1, feeType, content), 10));
        Assert.Empty(repository.Added);
    }

    [Fact]
    public async Task CreateAsync_limits_the_content_length()
    {
        var tooLong = new string('a', InvoiceComplaintService.MaxContentLength + 1);

        await Assert.ThrowsAsync<ComplaintBusinessRuleException>(() =>
            CreateService(new FakeComplaintRepository()).CreateAsync(new CreateInvoiceComplaintDto(1, RentLine, tooLong), 10));
    }

    [Fact]
    public async Task CreateAsync_rejects_someone_elses_or_a_missing_invoice()
    {
        await Assert.ThrowsAsync<ComplaintAuthorizationException>(() =>
            CreateService(new FakeComplaintRepository()).CreateAsync(new CreateInvoiceComplaintDto(1, RentLine, "Sai"), 20));
        await Assert.ThrowsAsync<ComplaintNotFoundException>(() =>
            CreateService(new FakeComplaintRepository()).CreateAsync(new CreateInvoiceComplaintDto(99, RentLine, "Sai"), 10));
    }

    [Fact]
    public async Task UpdateStatusAsync_lets_the_owner_resolve_and_notifies_the_tenant()
    {
        var repository = new FakeComplaintRepository { Complaint = SampleComplaintReadModel() };

        var updated = await CreateService(repository).UpdateStatusAsync(1, new UpdateComplaintStatusDto(ComplaintStatuses.Resolved), 7, RoleNames.Manager);

        var (complaintId, status, notification) = Assert.Single(repository.Updates);
        Assert.Equal((1, ComplaintStatuses.Resolved), (complaintId, status));
        Assert.Equal((10, NotificationTypes.Complaint), (notification.UserId, notification.Type));
        Assert.Contains("đã được xử lý", notification.Title);
        Assert.Equal(ComplaintStatuses.Resolved, updated.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_does_nothing_when_the_status_is_unchanged()
    {
        var repository = new FakeComplaintRepository { Complaint = SampleComplaintReadModel() };

        await CreateService(repository).UpdateStatusAsync(1, new UpdateComplaintStatusDto(ComplaintStatuses.New), 9, RoleNames.Admin);

        Assert.Empty(repository.Updates);
    }

    [Theory]
    [InlineData(RoleNames.Customer, 10, ComplaintStatuses.Resolved)]
    [InlineData(RoleNames.Manager, 8, ComplaintStatuses.Resolved)]
    public async Task UpdateStatusAsync_allows_only_the_owner_or_an_admin(string role, int userId, string status)
    {
        var repository = new FakeComplaintRepository { Complaint = SampleComplaintReadModel() };

        await Assert.ThrowsAsync<ComplaintAuthorizationException>(() =>
            CreateService(repository).UpdateStatusAsync(1, new UpdateComplaintStatusDto(status), userId, role));
        Assert.Empty(repository.Updates);
    }

    [Fact]
    public async Task UpdateStatusAsync_rejects_an_unknown_status()
    {
        await Assert.ThrowsAsync<ComplaintBusinessRuleException>(() =>
            CreateService(new FakeComplaintRepository { Complaint = SampleComplaintReadModel() })
                .UpdateStatusAsync(1, new UpdateComplaintStatusDto("Closed"), 9, RoleNames.Admin));
    }

    [Theory]
    [InlineData(RoleNames.Customer, null, 42, null)]
    [InlineData(RoleNames.Manager, ComplaintStatuses.New, null, 42)]
    [InlineData(RoleNames.Admin, ComplaintStatuses.Resolved, null, null)]
    public async Task GetAllAsync_scopes_by_role_and_filters_by_status(string role, string? status, int? expectedCustomerId, int? expectedOwnerId)
    {
        var repository = new FakeComplaintRepository();

        await CreateService(repository).GetAllAsync(42, role, status);

        Assert.Equal((expectedCustomerId, expectedOwnerId, status), repository.LastQuery);
    }

    private static InvoiceComplaintService CreateService(FakeComplaintRepository repository) => new(repository, new FixedTimeProvider(Now));

    private static InvoiceComplaintReadModel SampleComplaintReadModel() => new(
        1, 1, October, "A-101", "Sunrise", 10, "Customer A", RentLine, "Sai giá phòng", ComplaintStatuses.New, Now.UtcDateTime);

    private sealed class FakeComplaintRepository : IInvoiceComplaintRepository
    {
        public ComplaintInvoiceReadModel Invoice
        {
            get; init;
        } =
            new(1, 10, 7, "A-101", October, [RentLine, "Electricity - per kWh - tháng 10/2026"]);
        public InvoiceComplaintReadModel? Complaint
        {
            get; set;
        }
        public List<(InvoiceComplaints Complaint, Notifications Notification)> Added { get; } = [];
        public List<(int ComplaintId, string Status, Notifications Notification)> Updates { get; } = [];
        public (int? CustomerId, int? OwnerId, string? Status) LastQuery
        {
            get; private set;
        }

        public Task<ComplaintInvoiceReadModel?> GetInvoiceAsync(int invoiceId) =>
            Task.FromResult(Invoice.InvoiceId == invoiceId ? Invoice : null);

        public Task AddAsync(InvoiceComplaints complaint, Notifications ownerNotification)
        {
            complaint.ComplaintId = 1;
            Added.Add((complaint, ownerNotification));
            Complaint = new InvoiceComplaintReadModel(1, complaint.InvoiceId, October, "A-101", "Sunrise", complaint.CustomerId,
                "Customer A", complaint.FeeType, complaint.Content, complaint.Status, complaint.CreatedAt);
            return Task.CompletedTask;
        }

        public Task<List<InvoiceComplaintReadModel>> GetAllAsync(int? customerId, int? ownerId, string? status)
        {
            LastQuery = (customerId, ownerId, status);
            return Task.FromResult(new List<InvoiceComplaintReadModel>());
        }

        // Khiếu nại mẫu thuộc khách 10 ở khu trọ của chủ 7; ngoài phạm vi đó thì trả null như repository thật.
        public Task<InvoiceComplaintReadModel?> GetByIdAsync(int complaintId, int? customerId, int? ownerId)
        {
            var visible = Complaint?.ComplaintId == complaintId &&
                (customerId is null || Complaint.CustomerId == customerId) && (ownerId is null or 7);
            return Task.FromResult(visible ? Complaint : null);
        }

        public Task<bool> ExistsAsync(int complaintId) => Task.FromResult(Complaint?.ComplaintId == complaintId);

        public Task UpdateStatusAsync(int complaintId, string status, Notifications customerNotification)
        {
            Updates.Add((complaintId, status, customerNotification));
            Complaint = Complaint! with
            {
                Status = status
            };
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
