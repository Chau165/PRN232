using System.Globalization;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Policies;
using PRN232.Application.ReadModels;
using PRN232.Application.States;
using PRN232.Domain.Entities;

namespace PRN232.Application.Services;

public class InvoiceService : IInvoiceService
{
    // Tháng hoá đơn và hạn thanh toán tính theo lịch Việt Nam, giống hợp đồng và chỉ số.
    private static readonly TimeZoneInfo BusinessTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    private readonly IInvoiceRepository _repository;
    private readonly IPaymentGateway _paymentGateway;
    private readonly TimeProvider _timeProvider;
    private readonly INotificationRealtimePublisher? _realtimePublisher;

    public InvoiceService(
        IInvoiceRepository repository,
        IPaymentGateway paymentGateway,
        TimeProvider timeProvider,
        INotificationRealtimePublisher? realtimePublisher = null)
    {
        _repository = repository;
        _paymentGateway = paymentGateway;
        _timeProvider = timeProvider;
        _realtimePublisher = realtimePublisher;
    }

    // Xem trước và tạo thật dùng chung BuildAsync, nên số tiền xem trước luôn khớp hoá đơn được lưu.
    public async Task<InvoiceDto> PreviewAsync(CreateInvoiceDto dto, int userId, string role)
    {
        var (contract, invoice) = await BuildAsync(dto, userId, role);
        return ConvertToDto(invoice, contract, "Preview");
    }

    public async Task<InvoiceDto> CreateAsync(CreateInvoiceDto dto, int userId, string role)
    {
        var (contract, invoice) = await BuildAsync(dto, userId, role);
        var total = invoice.InvoiceDetails.Sum(detail => detail.Amount);
        var notification = new Notifications
        {
            UserId = contract.CustomerId,
            Title = $"Hoá đơn tháng {Period(invoice.InvoiceMonth)} - Phòng {contract.RoomCode}: {total.ToString("N0", Vietnamese)}đ",
            Type = NotificationTypes.Invoice,
            IsRead = false
        };
        if (!await _repository.AddIfAbsentAsync(invoice, notification, userId))
        {
            throw new InvoiceConflictException("An invoice for this month already exists.");
        }
        if (_realtimePublisher is not null)
        {
            await _realtimePublisher.PublishCreatedAsync([notification]);
        }
        var created = await _repository.GetByIdAsync(invoice.InvoiceId, null, null);
        if (created is null)
        {
            throw new InvalidOperationException("Created invoice was not found.");
        }
        return ConvertToDto(created);
    }

    public async Task<List<InvoiceDto>> GetAllAsync(int userId, string role, int? contractId, string? status)
    {
        if (status is not null && !InvoiceStatuses.IsValid(status))
        {
            throw new InvoiceBusinessRuleException("Status must be Unpaid, Paid, or Overdue.");
        }

        var (customerId, managerId) = GetScope(userId, role);
        var invoices = await _repository.GetAllAsync(customerId, managerId, contractId, status);
        return invoices.Select(ConvertToDto).ToList();
    }

    public async Task<InvoiceDto?> GetByIdAsync(int id, int userId, string role)
    {
        var (customerId, managerId) = GetScope(userId, role);
        var invoice = await _repository.GetByIdAsync(id, customerId, managerId);
        if (invoice is null && (customerId.HasValue || managerId.HasValue) && await _repository.ExistsAsync(id))
        {
            throw new InvoiceAuthorizationException("You cannot access this invoice.");
        }
        if (invoice is null)
        {
            return null;
        }
        return ConvertToDto(invoice);
    }

    // Khách tự thanh toán qua cổng giả lập (cùng cổng với tiền cọc ở bước đặt cọc).
    public async Task<InvoiceDto> PayAsync(int id, PayInvoiceDto dto, int customerId)
    {
        if (!PaymentMethods.IsValid(dto.PaymentMethod))
        {
            throw new InvoiceBusinessRuleException("Payment method must be VNPay or MoMo.");
        }

        var invoice = await GetInvoiceForPaymentAsync(id);
        if (invoice.CustomerId != customerId)
        {
            throw new InvoiceAuthorizationException("You cannot pay another customer's invoice.");
        }
        EnsureNotPaid(invoice);
        var notification = new Notifications
        {
            UserId = invoice.OwnerId,
            Title = $"Phòng {invoice.RoomCode} đã thanh toán hoá đơn tháng {Period(invoice.InvoiceMonth)}",
            Type = NotificationTypes.Invoice,
            IsRead = false
        };
        var result = await _repository.TryPayOnlineAsync(
            id,
            customerId,
            dto.PaymentMethod,
            amount => _paymentGateway.ChargeAsync(dto.PaymentMethod, amount),
            notification);
        return result switch
        {
            InvoicePaymentAttemptStatus.Completed => await PublishPaymentAndLoadAsync(id, notification),
            InvoicePaymentAttemptStatus.NoAmountDue => await LoadSavedInvoiceAsync(id),
            InvoicePaymentAttemptStatus.Declined => throw new InvoicePaymentFailedException("The payment was declined."),
            InvoicePaymentAttemptStatus.NotFound => throw new InvoiceNotFoundException("Invoice does not exist."),
            InvoicePaymentAttemptStatus.Forbidden => throw new InvoiceAuthorizationException("You cannot pay another customer's invoice."),
            _ => throw new InvoiceConflictException("This invoice has already been paid.")
        };
    }

    // Quản lý ghi nhận khách đã trả tiền mặt tại khu trọ (thay cho nút "Đã thu" mock trước đây).
    public async Task<InvoiceDto> RecordCashPaymentAsync(int id, int userId, string role)
    {
        if (role != RoleNames.Manager)
        {
            throw new InvoiceAuthorizationException("Only an assigned property manager can record cash payments.");
        }

        var invoice = await GetInvoiceForPaymentAsync(id);
        if (!await _repository.HasActiveManagerAssignmentAsync(invoice.PropertyId, userId))
        {
            throw new InvoiceAuthorizationException("You cannot record payments for this invoice.");
        }
        EnsureNotPaid(invoice);

        var title = invoice.Total <= 0m
            ? $"Hoá đơn tháng {Period(invoice.InvoiceMonth)} - Phòng {invoice.RoomCode} đã được bù trừ bằng tiền trả trước"
            : $"Đã ghi nhận thanh toán tiền mặt hoá đơn tháng {Period(invoice.InvoiceMonth)} - Phòng {invoice.RoomCode}";
        return await MarkPaidAsync(invoice, PaymentMethods.Cash, invoice.CustomerId,
            title,
            userId);
    }

    private async Task<InvoicePaymentReadModel> GetInvoiceForPaymentAsync(int id)
    {
        var invoice = await _repository.GetForPaymentAsync(id);
        if (invoice is null)
        {
            throw new InvoiceNotFoundException("Invoice does not exist.");
        }
        return invoice;
    }

    // Gọi sau khi đã kiểm tra quyền: người ngoài phạm vi phải nhận 403, không biết hoá đơn đã trả hay chưa.
    private static void EnsureNotPaid(InvoicePaymentReadModel invoice)
    {
        if (invoice.Status == InvoiceStatuses.Paid)
        {
            throw new InvoiceConflictException("This invoice has already been paid.");
        }
    }

    // Người trả luôn là khách thuê; số tiền là tổng các dòng do server tính, client không gửi số tiền.
    private async Task<InvoiceDto> MarkPaidAsync(
        InvoicePaymentReadModel invoice,
        string paymentMethod,
        int notifyUserId,
        string notificationTitle,
        int? managerId = null)
    {
        var payment = new Payments
        {
            UserId = invoice.CustomerId,
            InvoiceId = invoice.InvoiceId,
            Amount = invoice.Total,
            PaymentMethod = paymentMethod,
            Status = PaymentStatuses.Success
        };
        var notification = new Notifications
        {
            UserId = notifyUserId,
            Title = notificationTitle,
            Type = NotificationTypes.Invoice,
            IsRead = false
        };
        // Repository chỉ chuyển sang Paid khi hoá đơn vẫn chưa thanh toán: hai lần trả cùng lúc thì chỉ một lần được ghi.
        if (!await _repository.MarkPaidAsync(invoice.InvoiceId, payment, notification, managerId))
        {
            if (managerId.HasValue &&
                !await _repository.HasActiveManagerAssignmentAsync(invoice.PropertyId, managerId.Value))
            {
                throw new InvoiceAuthorizationException("You are no longer assigned to manage this property's invoices.");
            }
            throw new InvoiceConflictException("This invoice has already been paid.");
        }
        return await PublishPaymentAndLoadAsync(invoice.InvoiceId, notification);
    }

    private async Task<InvoiceDto> PublishPaymentAndLoadAsync(int invoiceId, Notifications notification)
    {
        if (_realtimePublisher is not null)
        {
            await _realtimePublisher.PublishCreatedAsync([notification]);
        }
        return await LoadSavedInvoiceAsync(invoiceId);
    }

    private async Task<InvoiceDto> LoadSavedInvoiceAsync(int invoiceId)
    {
        var paid = await _repository.GetByIdAsync(invoiceId, null, null);
        if (paid is null)
        {
            throw new InvalidOperationException("Paid invoice was not found.");
        }
        return ConvertToDto(paid);
    }

    private async Task<(InvoiceContractReadModel Contract, Invoices Invoice)> BuildAsync(CreateInvoiceDto dto, int userId, string role)
    {
        if (role != RoleNames.Manager)
        {
            throw new InvoiceAuthorizationException("Only an assigned property manager can create invoices.");
        }

        var contract = await _repository.GetContractAsync(dto.ContractId);
        if (contract is null)
        {
            throw new InvoiceNotFoundException("Contract does not exist.");
        }
        if (!await _repository.HasActiveManagerAssignmentAsync(contract.PropertyId, userId))
        {
            throw new InvoiceAuthorizationException("You cannot create invoices for this contract.");
        }
        if (contract.Status != ContractStatuses.Active)
        {
            throw new InvoiceConflictException("Invoices can only be created for active contracts.");
        }

        EnsureValidDates(dto, contract);
        if (await _repository.ExistsForMonthAsync(contract.ContractId, dto.InvoiceMonth))
        {
            throw new InvoiceConflictException("An invoice for this month already exists.");
        }

        var period = Period(dto.InvoiceMonth);
        var details = new List<InvoiceDetails>
        {
            new InvoiceDetails()
            {
                ItemType = InvoiceItemTypes.Rent,
                Description = $"Tiền phòng tháng {period}",
                Amount = contract.MonthlyRent
            }
        };

        var services = await _repository.GetBillableServicesAsync(contract.PropertyId, contract.RoomTypeId, dto.InvoiceMonth);
        MonthUsageReadModel? usage = null;
        if (services.Any(service => service.BillingBasis == "usage"))
        {
            usage = await _repository.GetUsageAsync(contract.ContractId, dto.InvoiceMonth);
            if (usage is null)
            {
                throw new InvoiceConflictException("Record this month's meter readings before creating the invoice.");
            }
        }

        foreach (var service in services)
        {
            details.Add(BuildServiceLine(service, usage, period));
        }

        var invoiceMonth = new DateOnly(contract.StartDate.Year, contract.StartDate.Month, 1);
        if (dto.InvoiceMonth == invoiceMonth && contract.BookingId.HasValue)
        {
            var credit = await _repository.GetAvailablePrepaidRentCreditAsync(contract.ContractId);
            if (credit is not null)
            {
                var prepaidRentApplied = Math.Min(
                    credit.AvailableAmount,
                    details.Where(detail => detail.ItemType == InvoiceItemTypes.Rent)
                        .Sum(detail => detail.Amount));
                if (prepaidRentApplied > 0m)
                {
                    details.Add(new InvoiceDetails
                    {
                        ItemType = InvoiceItemTypes.PrepaidRentCredit,
                        Description = $"Tiền cọc đã thanh toán - khấu trừ tiền phòng tháng {period}",
                        Amount = -prepaidRentApplied
                    });
                }
            }
        }

        var invoice = new Invoices
        {
            ContractId = contract.ContractId,
            InvoiceMonth = dto.InvoiceMonth,
            DueDate = dto.DueDate,
            Status = InvoiceStatuses.Unpaid,
            InvoiceDetails = details
        };
        return (contract, invoice);
    }

    // Mỗi dòng lưu lại RateId, số lượng, đơn giá và đơn vị lúc lập: khu trọ đổi giá sau này
    // thì hoá đơn cũ vẫn giữ nguyên số tiền đã tính.
    private static InvoiceDetails BuildServiceLine(BillableServiceReadModel service, MonthUsageReadModel? usage, string period)
    {
        if (service.RateId is null || service.Price is null)
        {
            throw new InvoiceConflictException($"Service \"{service.ServiceName}\" has no price for this month.");
        }

        var (itemType, quantity) = (service.UnitCode, service.BillingBasis) switch
        {
            ("kwh", "usage") => (InvoiceItemTypes.Electricity, usage!.ElectricUsage),
            ("m3", "usage") => (InvoiceItemTypes.Water, usage!.WaterUsage),
            (_, "fixed_room") => (InvoiceItemTypes.Service, 1m),
            _ => throw new InvoiceBusinessRuleException(
                $"Service \"{service.ServiceName}\" is billed per person or vehicle, which is not supported yet.")
        };

        return new InvoiceDetails
        {
            ItemType = itemType,
            Description = $"{service.ServiceName} - tháng {period}",
            ServiceId = service.ServiceId,
            RateId = service.RateId,
            Quantity = quantity,
            UnitPrice = service.Price,
            UnitCode = service.UnitCode,
            BillingBasis = service.BillingBasis,
            Amount = Math.Round(quantity * service.Price.Value, 0, MidpointRounding.AwayFromZero)
        };
    }

    private void EnsureValidDates(CreateInvoiceDto dto, InvoiceContractReadModel contract)
    {
        if (dto.InvoiceMonth.Day != 1)
        {
            throw new InvoiceBusinessRuleException("Invoice month must be the first day of a month.");
        }

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), BusinessTimeZone).DateTime);
        if (dto.InvoiceMonth > new DateOnly(today.Year, today.Month, 1))
        {
            throw new InvoiceBusinessRuleException("Cannot create an invoice for a future month.");
        }
        if (dto.InvoiceMonth < new DateOnly(contract.StartDate.Year, contract.StartDate.Month, 1) ||
            dto.InvoiceMonth > contract.EndDate)
        {
            throw new InvoiceBusinessRuleException("Invoice month must be within the contract term.");
        }
        if (dto.DueDate < today)
        {
            throw new InvoiceBusinessRuleException("Due date cannot be in the past.");
        }
    }

    private static InvoiceDto ConvertToDto(Invoices invoice, InvoiceContractReadModel contract, string status)
    {
        var total = invoice.InvoiceDetails.Sum(detail => detail.Amount);
        var prepaidRentApplied = invoice.InvoiceDetails
            .Where(detail => detail.ItemType == InvoiceItemTypes.PrepaidRentCredit && detail.Amount < 0m)
            .Sum(detail => -detail.Amount);
        var netPayable = Math.Max(0m, total);
        return new InvoiceDto(
            invoice.InvoiceId,
            contract.ContractId,
            contract.RoomCode,
            contract.PropertyName,
            contract.CustomerName,
            invoice.InvoiceMonth,
            invoice.DueDate,
            status,
            total,
            invoice.InvoiceDetails
            .Select(ConvertToDto)
            .ToList(),
            prepaidRentApplied,
            netPayable,
            0m,
            netPayable);
    }

    private static InvoiceDto ConvertToDto(InvoiceReadModel invoice)
    {
        return new InvoiceDto(
            invoice.InvoiceId,
            invoice.ContractId,
            invoice.RoomCode,
            invoice.PropertyName,
            invoice.CustomerName,
            invoice.InvoiceMonth,
            invoice.DueDate,
            invoice.Status,
            invoice.Total,
            invoice.Lines.Select(ConvertToDto).ToList(),
            invoice.PrepaidRentApplied,
            invoice.NetPayable,
            invoice.AmountPaid,
            invoice.RemainingPayable);
    }

    private static InvoiceLineDto ConvertToDto(InvoiceDetails detail)
    {
        return new InvoiceLineDto(
            detail.ItemType, detail.Description, detail.Quantity, detail.UnitPrice, detail.UnitCode, detail.Amount);
    }

    private static InvoiceLineDto ConvertToDto(InvoiceLineReadModel line)
    {
        return new InvoiceLineDto(
            line.ItemType, line.Description, line.Quantity, line.UnitPrice, line.UnitCode, line.Amount);
    }

    private static string Period(DateOnly month)
    {
        return month.ToString("MM/yyyy", CultureInfo.InvariantCulture);
    }

    private static (int? CustomerId, int? ManagerId) GetScope(int userId, string role)
    {
        var scope = AccessScope.For(userId, role);
        if (scope is null)
        {
            throw new InvoiceAuthorizationException("Your role cannot access invoices.");
        }
        return scope.Value;
    }
}

public sealed class InvoiceNotFoundException(string message) : Exception(message);

public sealed class InvoiceAuthorizationException(string message) : Exception(message);

public sealed class InvoiceBusinessRuleException(string message) : Exception(message);

public sealed class InvoiceConflictException(string message) : Exception(message);

public sealed class InvoicePaymentFailedException(string message) : Exception(message);
