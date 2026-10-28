using System.Globalization;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Policies;
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

    public InvoiceService(IInvoiceRepository repository, IPaymentGateway paymentGateway, TimeProvider timeProvider)
    {
        _repository = repository;
        _paymentGateway = paymentGateway;
        _timeProvider = timeProvider;
    }

    // Xem trước và tạo thật dùng chung BuildAsync, nên số tiền xem trước luôn khớp hoá đơn được lưu.
    public async Task<InvoiceDto> PreviewAsync(CreateInvoiceDto dto, int userId, string role)
    {
        var (contract, invoice) = await BuildAsync(dto, userId, role);
        return ToDto(invoice, contract, "Preview");
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
        if (!await _repository.AddIfAbsentAsync(invoice, notification))
        {
            throw new InvoiceConflictException("An invoice for this month already exists.");
        }

        return await _repository.GetByIdAsync(invoice.InvoiceId, null, null)
            ?? throw new InvalidOperationException("Created invoice was not found.");
    }

    public Task<List<InvoiceDto>> GetAllAsync(int userId, string role, int? contractId, string? status)
    {
        if (status is not null && !InvoiceStatuses.IsValid(status))
        {
            throw new InvoiceBusinessRuleException("Status must be Unpaid, Paid, or Overdue.");
        }

        var (customerId, ownerId) = GetScope(userId, role);
        return _repository.GetAllAsync(customerId, ownerId, contractId, status);
    }

    public async Task<InvoiceDto?> GetByIdAsync(int id, int userId, string role)
    {
        var (customerId, ownerId) = GetScope(userId, role);
        var invoice = await _repository.GetByIdAsync(id, customerId, ownerId);
        if (invoice is null && (customerId.HasValue || ownerId.HasValue) && await _repository.ExistsAsync(id))
        {
            throw new InvoiceAuthorizationException("You cannot access this invoice.");
        }
        return invoice;
    }

    // Khách tự thanh toán qua cổng giả lập (cùng cổng với tiền cọc ở bước đặt cọc).
    public async Task<InvoiceDto> PayAsync(int id, PayInvoiceDto dto, int customerId)
    {
        if (!PaymentMethods.IsValid(dto.PaymentMethod))
        {
            throw new InvoiceBusinessRuleException("Payment method must be VNPay or MoMo.");
        }

        var invoice = await GetUnpaidForPaymentAsync(id);
        if (invoice.CustomerId != customerId)
        {
            throw new InvoiceAuthorizationException("You cannot pay another customer's invoice.");
        }
        if (!await _paymentGateway.ChargeAsync(dto.PaymentMethod, invoice.Total))
        {
            throw new InvoicePaymentFailedException("The payment was declined.");
        }

        return await MarkPaidAsync(invoice, dto.PaymentMethod, invoice.OwnerId,
            $"Phòng {invoice.RoomCode} đã thanh toán hoá đơn tháng {Period(invoice.InvoiceMonth)}");
    }

    // Quản lý ghi nhận khách đã trả tiền mặt tại khu trọ (thay cho nút "Đã thu" mock trước đây).
    public async Task<InvoiceDto> RecordCashPaymentAsync(int id, int userId, string role)
    {
        if (role is not (RoleNames.Manager or RoleNames.Admin))
        {
            throw new InvoiceAuthorizationException("Only property managers can record cash payments.");
        }

        var invoice = await GetUnpaidForPaymentAsync(id);
        if (role == RoleNames.Manager && invoice.OwnerId != userId)
        {
            throw new InvoiceAuthorizationException("You cannot record payments for this invoice.");
        }

        return await MarkPaidAsync(invoice, PaymentMethods.Cash, invoice.CustomerId,
            $"Đã ghi nhận thanh toán tiền mặt hoá đơn tháng {Period(invoice.InvoiceMonth)} - Phòng {invoice.RoomCode}");
    }

    private async Task<InvoicePaymentDto> GetUnpaidForPaymentAsync(int id)
    {
        var invoice = await _repository.GetForPaymentAsync(id)
            ?? throw new InvoiceNotFoundException("Invoice does not exist.");
        if (invoice.Status == InvoiceStatuses.Paid)
        {
            throw new InvoiceConflictException("This invoice has already been paid.");
        }
        return invoice;
    }

    // Người trả luôn là khách thuê; số tiền là tổng các dòng do server tính, client không gửi số tiền.
    private async Task<InvoiceDto> MarkPaidAsync(
        InvoicePaymentDto invoice, string paymentMethod, int notifyUserId, string notificationTitle)
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
        if (!await _repository.MarkPaidAsync(invoice.InvoiceId, payment, notification))
        {
            throw new InvoiceConflictException("This invoice has already been paid.");
        }

        return await _repository.GetByIdAsync(invoice.InvoiceId, null, null)
            ?? throw new InvalidOperationException("Paid invoice was not found.");
    }

    private async Task<(InvoiceContractDto Contract, Invoices Invoice)> BuildAsync(CreateInvoiceDto dto, int userId, string role)
    {
        if (role is not (RoleNames.Manager or RoleNames.Admin))
        {
            throw new InvoiceAuthorizationException("Only property managers can create invoices.");
        }

        var contract = await _repository.GetContractAsync(dto.ContractId)
            ?? throw new InvoiceNotFoundException("Contract does not exist.");
        if (role == RoleNames.Manager && contract.OwnerId != userId)
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
            new()
            {
                ItemType = InvoiceItemTypes.Rent,
                Description = $"Tiền phòng tháng {period}",
                Amount = contract.MonthlyRent
            }
        };

        var services = await _repository.GetBillableServicesAsync(contract.PropertyId, contract.RoomTypeId, dto.InvoiceMonth);
        MonthUsageDto? usage = null;
        if (services.Any(service => service.BillingBasis == "usage"))
        {
            usage = await _repository.GetUsageAsync(contract.ContractId, dto.InvoiceMonth)
                ?? throw new InvoiceConflictException("Record this month's meter readings before creating the invoice.");
        }

        foreach (var service in services)
        {
            details.Add(BuildServiceLine(service, usage, period));
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
    private static InvoiceDetails BuildServiceLine(BillableServiceDto service, MonthUsageDto? usage, string period)
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

    private void EnsureValidDates(CreateInvoiceDto dto, InvoiceContractDto contract)
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

    private static InvoiceDto ToDto(Invoices invoice, InvoiceContractDto contract, string status) => new(
        invoice.InvoiceId,
        contract.ContractId,
        contract.RoomCode,
        contract.PropertyName,
        contract.CustomerName,
        invoice.InvoiceMonth,
        invoice.DueDate,
        status,
        invoice.InvoiceDetails.Sum(detail => detail.Amount),
        invoice.InvoiceDetails
            .Select(detail => new InvoiceLineDto(
                detail.ItemType, detail.Description, detail.Quantity, detail.UnitPrice, detail.UnitCode, detail.Amount))
            .ToList());

    private static string Period(DateOnly month) => month.ToString("MM/yyyy", CultureInfo.InvariantCulture);

    private static (int? CustomerId, int? OwnerId) GetScope(int userId, string role) =>
        AccessScope.For(userId, role) ?? throw new InvoiceAuthorizationException("Your role cannot access invoices.");
}

public sealed class InvoiceNotFoundException(string message) : Exception(message);

public sealed class InvoiceAuthorizationException(string message) : Exception(message);

public sealed class InvoiceBusinessRuleException(string message) : Exception(message);

public sealed class InvoiceConflictException(string message) : Exception(message);

public sealed class InvoicePaymentFailedException(string message) : Exception(message);
