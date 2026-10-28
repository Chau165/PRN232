using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Policies;
using PRN232.Application.States;
using PRN232.Domain.Entities;

namespace PRN232.Application.Services;

public class ContractService : IContractService
{
    // Ngày hợp đồng là ngày theo lịch Việt Nam, giống cách RentalService xác định "đang thuê".
    private static readonly TimeZoneInfo BusinessTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    private readonly IContractRepository _repository;
    private readonly TimeProvider _timeProvider;

    public ContractService(IContractRepository repository, TimeProvider timeProvider)
    {
        _repository = repository;
        _timeProvider = timeProvider;
    }

    public async Task<ContractDto> CreateFromBookingAsync(CreateContractDto dto, int userId, string role)
    {
        if (role is not (RoleNames.Manager or RoleNames.Admin))
        {
            throw new ContractAuthorizationException("Only property managers can create contracts.");
        }

        var booking = await _repository.GetBookingAsync(dto.BookingId)
            ?? throw new ContractNotFoundException("Booking does not exist.");
        if (role == RoleNames.Manager && booking.OwnerId != userId)
        {
            throw new ContractAuthorizationException("You cannot create a contract for this booking.");
        }
        if (booking.Status != BookingStatuses.Deposited)
        {
            throw new ContractConflictException("Only deposited bookings can become contracts.");
        }
        if (booking.HasContract)
        {
            throw new ContractConflictException("This booking already has a contract.");
        }

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), BusinessTimeZone).DateTime);
        if (dto.StartDate < today)
        {
            throw new ContractBusinessRuleException("Start date cannot be in the past.");
        }
        if (dto.EndDate <= dto.StartDate)
        {
            throw new ContractBusinessRuleException("End date must be after the start date.");
        }

        // Giá thuê chốt theo giá phòng lúc lập hợp đồng; tiền cọc là số khách đã trả cho booking.
        var contract = new Contracts
        {
            RoomId = booking.RoomId,
            CustomerId = booking.CustomerId,
            BookingId = booking.BookingId,
            ProcessedByStaffId = userId,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            MonthlyRent = booking.MonthlyRent,
            DepositAmount = booking.DepositAmount,
            Status = ContractStatuses.Active
        };
        var notification = new Notifications
        {
            UserId = booking.CustomerId,
            Title = $"Hợp đồng thuê phòng {booking.RoomCode} đã được lập",
            Type = NotificationTypes.Contract,
            IsRead = false
        };
        if (!await _repository.AddFromBookingAsync(contract, notification))
        {
            throw new ContractConflictException("The booking changed or the room already has an active contract.");
        }

        return await _repository.GetByIdAsync(contract.ContractId, null, null)
            ?? throw new InvalidOperationException("Created contract was not found.");
    }

    public Task<List<ContractDto>> GetAllAsync(int userId, string role)
    {
        var (customerId, ownerId) = GetScope(userId, role);
        return _repository.GetAllAsync(customerId, ownerId);
    }

    public async Task<ContractDto?> GetByIdAsync(int id, int userId, string role)
    {
        var (customerId, ownerId) = GetScope(userId, role);
        var contract = await _repository.GetByIdAsync(id, customerId, ownerId);
        if (contract is null && (customerId.HasValue || ownerId.HasValue) && await _repository.ExistsAsync(id))
        {
            throw new ContractAuthorizationException("You cannot access this contract.");
        }
        return contract;
    }

    private static (int? CustomerId, int? OwnerId) GetScope(int userId, string role) =>
        AccessScope.For(userId, role) ?? throw new ContractAuthorizationException("Your role cannot access contracts.");
}

public sealed class ContractNotFoundException(string message) : Exception(message);

public sealed class ContractAuthorizationException(string message) : Exception(message);

public sealed class ContractBusinessRuleException(string message) : Exception(message);

public sealed class ContractConflictException(string message) : Exception(message);
