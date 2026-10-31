using System.Globalization;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Policies;
using PRN232.Application.ReadModels;
using PRN232.Application.States;
using PRN232.Domain.Entities;

namespace PRN232.Application.Services;

public class InvoiceComplaintService : IInvoiceComplaintService
{
    public const int MaxContentLength = 1000;

    private readonly IInvoiceComplaintRepository _repository;
    private readonly TimeProvider _timeProvider;

    public InvoiceComplaintService(IInvoiceComplaintRepository repository, TimeProvider timeProvider)
    {
        _repository = repository;
        _timeProvider = timeProvider;
    }

    public async Task<InvoiceComplaintDto> CreateAsync(CreateInvoiceComplaintDto dto, int customerId)
    {
        var feeType = dto.FeeType?.Trim() ?? string.Empty;
        var content = dto.Content?.Trim() ?? string.Empty;
        if (content.Length == 0 || content.Length > MaxContentLength)
        {
            throw new ComplaintBusinessRuleException($"Content is required and must be at most {MaxContentLength} characters.");
        }

        var invoice = await _repository.GetInvoiceAsync(dto.InvoiceId);
        if (invoice is null)
        {
            throw new ComplaintNotFoundException("Invoice does not exist.");
        }
        if (invoice.CustomerId != customerId)
        {
            throw new ComplaintAuthorizationException("You cannot complain about another customer's invoice.");
        }
        // Loại phí phải là một dòng có thật trên chính hoá đơn này, để quản lý biết chính xác khoản bị khiếu nại.
        if (feeType != ComplaintStatuses.OtherFeeType && !invoice.LineDescriptions.Contains(feeType))
        {
            throw new ComplaintBusinessRuleException("Fee type must be one of the invoice lines or \"Khác\".");
        }

        var complaint = CreateComplaintEntity(invoice.InvoiceId, customerId, feeType, content, _timeProvider.GetUtcNow().UtcDateTime);
        await _repository.AddAsync(complaint, new Notifications
        {
            UserId = invoice.OwnerId,
            Title = $"Khách khiếu nại hoá đơn tháng {Period(invoice.InvoiceMonth)} - Phòng {invoice.RoomCode}",
            Type = NotificationTypes.Complaint,
            IsRead = false
        });

        var created = await _repository.GetByIdAsync(complaint.ComplaintId, customerId, null);
        if (created is null)
        {
            throw new InvalidOperationException("Created complaint was not found.");
        }
        return ConvertToDto(created);
    }

    public async Task<List<InvoiceComplaintDto>> GetAllAsync(int userId, string role, string? status)
    {
        if (status is not null && !ComplaintStatuses.IsValid(status))
        {
            throw new ComplaintBusinessRuleException("Status must be New, InProgress, or Resolved.");
        }

        var (customerId, ownerId) = GetScope(userId, role);
        var complaints = await _repository.GetAllAsync(customerId, ownerId, status);
        return complaints.Select(ConvertToDto).ToList();
    }

    public async Task<InvoiceComplaintDto?> GetByIdAsync(int id, int userId, string role)
    {
        var (customerId, ownerId) = GetScope(userId, role);
        var complaint = await _repository.GetByIdAsync(id, customerId, ownerId);
        if (complaint is null && (customerId.HasValue || ownerId.HasValue) && await _repository.ExistsAsync(id))
        {
            throw new ComplaintAuthorizationException("You cannot access this complaint.");
        }
        if (complaint is null)
        {
            return null;
        }
        return ConvertToDto(complaint);
    }

    public async Task<InvoiceComplaintDto> UpdateStatusAsync(int id, UpdateComplaintStatusDto dto, int userId, string role)
    {
        if (role is not (RoleNames.Manager or RoleNames.Admin))
        {
            throw new ComplaintAuthorizationException("Only property managers can handle complaints.");
        }
        if (!ComplaintStatuses.IsValid(dto.Status))
        {
            throw new ComplaintBusinessRuleException("Status must be New, InProgress, or Resolved.");
        }

        var complaint = await GetByIdAsync(id, userId, role);
        if (complaint is null)
        {
            throw new ComplaintNotFoundException("Complaint does not exist.");
        }
        if (complaint.Status == dto.Status)
        {
            return complaint;
        }

        var label = dto.Status switch
        {
            ComplaintStatuses.InProgress => "đang được xử lý",
            ComplaintStatuses.Resolved => "đã được xử lý",
            _ => "được mở lại"
        };
        await _repository.UpdateStatusAsync(complaint.ComplaintId, dto.Status, new Notifications
        {
            UserId = complaint.CustomerId,
            Title = $"Khiếu nại hoá đơn tháng {Period(complaint.InvoiceMonth)} - Phòng {complaint.RoomCode} {label}",
            Type = NotificationTypes.Complaint,
            IsRead = false
        });

        var updated = await _repository.GetByIdAsync(complaint.ComplaintId, null, null);
        if (updated is null)
        {
            throw new InvalidOperationException("Updated complaint was not found.");
        }
        return ConvertToDto(updated);
    }

    private static string Period(DateOnly month)
    {
        return month.ToString("MM/yyyy", CultureInfo.InvariantCulture);
    }

    private static InvoiceComplaintDto ConvertToDto(InvoiceComplaintReadModel complaint)
    {
        return new InvoiceComplaintDto(
            complaint.ComplaintId,
            complaint.InvoiceId,
            complaint.InvoiceMonth,
            complaint.RoomCode,
            complaint.PropertyName,
            complaint.CustomerId,
            complaint.CustomerName,
            complaint.FeeType,
            complaint.Content,
            complaint.Status,
            complaint.CreatedAt);
    }

    private static InvoiceComplaints CreateComplaintEntity(
        int invoiceId, int customerId, string feeType, string content, DateTime createdAt)
    {
        return new InvoiceComplaints
        {
            InvoiceId = invoiceId,
            CustomerId = customerId,
            FeeType = feeType,
            Content = content,
            Status = ComplaintStatuses.New,
            CreatedAt = createdAt
        };
    }

    private static (int? CustomerId, int? OwnerId) GetScope(int userId, string role)
    {
        var scope = AccessScope.For(userId, role);
        if (scope is null)
        {
            throw new ComplaintAuthorizationException("Your role cannot access complaints.");
        }
        return scope.Value;
    }
}

public sealed class ComplaintNotFoundException(string message) : Exception(message);

public sealed class ComplaintAuthorizationException(string message) : Exception(message);

public sealed class ComplaintBusinessRuleException(string message) : Exception(message);
