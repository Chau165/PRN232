using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Policies;
using PRN232.Application.ReadModels;
using PRN232.Application.States;
using PRN232.Domain.Entities;

namespace PRN232.Application.Services;

public class UtilityReadingService : IUtilityReadingService
{
    // Tháng ghi chỉ số tính theo lịch Việt Nam, giống ngày hợp đồng.
    private static readonly TimeZoneInfo BusinessTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    private readonly IUtilityReadingRepository _repository;
    private readonly TimeProvider _timeProvider;

    public UtilityReadingService(IUtilityReadingRepository repository, TimeProvider timeProvider)
    {
        _repository = repository;
        _timeProvider = timeProvider;
    }

    public async Task<UtilityReadingDto> CreateAsync(CreateUtilityReadingDto dto, int userId, string role)
    {
        if (role is not (RoleNames.Manager or RoleNames.Admin))
        {
            throw new UtilityReadingAuthorizationException("Only property managers can record meter readings.");
        }

        var contract = await _repository.GetContractAsync(dto.ContractId)
            ?? throw new UtilityReadingNotFoundException("Contract does not exist.");
        if (role == RoleNames.Manager && contract.OwnerId != userId)
        {
            throw new UtilityReadingAuthorizationException("You cannot record readings for this contract.");
        }
        if (contract.Status != ContractStatuses.Active)
        {
            throw new UtilityReadingConflictException("Readings can only be recorded for active contracts.");
        }

        EnsureValidMonth(dto.ReadingMonth, contract);

        // Chỉ số đầu kỳ nối tiếp chỉ số cuối kỳ của lần ghi gần nhất, để chuỗi chỉ số không bị đứt
        // và quản lý không thể nhập sai số đầu; chỉ lần ghi đầu tiên của hợp đồng mới cần nhập.
        var latest = await _repository.GetLatestAsync(contract.ContractId);
        if (latest is not null && dto.ReadingMonth <= latest.ReadingMonth)
        {
            throw new UtilityReadingConflictException("A reading for this or a later month already exists.");
        }
        var electricStart = latest?.ElectricEnd ?? dto.ElectricStart
            ?? throw new UtilityReadingBusinessRuleException("The first reading of a contract needs the starting meter values.");
        var waterStart = latest?.WaterEnd ?? dto.WaterStart
            ?? throw new UtilityReadingBusinessRuleException("The first reading of a contract needs the starting meter values.");
        if (electricStart < 0 || waterStart < 0)
        {
            throw new UtilityReadingBusinessRuleException("Meter values cannot be negative.");
        }
        if (dto.ElectricEnd < electricStart || dto.WaterEnd < waterStart)
        {
            throw new UtilityReadingBusinessRuleException("New meter values cannot be lower than the previous ones.");
        }

        var reading = ConvertToEntity(dto, contract.ContractId, electricStart, waterStart);
        // Repository kiểm tra lại "chưa có tháng này hoặc tháng sau" ngay lúc ghi,
        // phòng khi hai người cùng nhập chỉ số cho một hợp đồng.
        if (!await _repository.AddIfLatestAsync(reading))
        {
            throw new UtilityReadingConflictException("A reading for this or a later month already exists.");
        }

        var created = await _repository.GetByIdAsync(reading.ReadingId, null, null)
            ?? throw new InvalidOperationException("Created reading was not found.");
        return ConvertToDto(created);
    }

    public async Task<List<UtilityReadingDto>> GetAllAsync(int userId, string role, int? contractId)
    {
        var (customerId, ownerId) = GetScope(userId, role);
        var readings = await _repository.GetAllAsync(customerId, ownerId, contractId);
        return readings.Select(ConvertToDto).ToList();
    }

    public async Task<UtilityReadingDto?> GetByIdAsync(int id, int userId, string role)
    {
        var (customerId, ownerId) = GetScope(userId, role);
        var reading = await _repository.GetByIdAsync(id, customerId, ownerId);
        if (reading is null && (customerId.HasValue || ownerId.HasValue) && await _repository.ExistsAsync(id))
        {
            throw new UtilityReadingAuthorizationException("You cannot access this reading.");
        }
        return reading is null ? null : ConvertToDto(reading);
    }

    private void EnsureValidMonth(DateOnly month, ReadingContractReadModel contract)
    {
        if (month.Day != 1)
        {
            throw new UtilityReadingBusinessRuleException("Reading month must be the first day of a month.");
        }

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), BusinessTimeZone).DateTime);
        if (month > new DateOnly(today.Year, today.Month, 1))
        {
            throw new UtilityReadingBusinessRuleException("Cannot record readings for a future month.");
        }
        if (month < new DateOnly(contract.StartDate.Year, contract.StartDate.Month, 1) || month > contract.EndDate)
        {
            throw new UtilityReadingBusinessRuleException("Reading month must be within the contract term.");
        }
    }

    private static UtilityReadings ConvertToEntity(
        CreateUtilityReadingDto dto, int contractId, decimal electricStart, decimal waterStart)
    {
        return new UtilityReadings
        {
            ContractId = contractId,
            ReadingMonth = dto.ReadingMonth,
            ElectricStart = electricStart,
            ElectricEnd = dto.ElectricEnd,
            WaterStart = waterStart,
            WaterEnd = dto.WaterEnd
        };
    }

    private static (int? CustomerId, int? OwnerId) GetScope(int userId, string role) =>
        AccessScope.For(userId, role) ?? throw new UtilityReadingAuthorizationException("Your role cannot access readings.");

    private static UtilityReadingDto ConvertToDto(UtilityReadingReadModel reading) => new(
        reading.ReadingId,
        reading.ContractId,
        reading.RoomCode,
        reading.PropertyName,
        reading.CustomerName,
        reading.ReadingMonth,
        reading.ElectricStart,
        reading.ElectricEnd,
        reading.ElectricUsage,
        reading.WaterStart,
        reading.WaterEnd,
        reading.WaterUsage);
}

public sealed class UtilityReadingNotFoundException(string message) : Exception(message);

public sealed class UtilityReadingAuthorizationException(string message) : Exception(message);

public sealed class UtilityReadingBusinessRuleException(string message) : Exception(message);

public sealed class UtilityReadingConflictException(string message) : Exception(message);
