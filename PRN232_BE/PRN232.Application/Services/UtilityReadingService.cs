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
        var (reading, warnings, _) = await PrepareAsync(dto, userId, role);
        // Repository kiểm tra lại "chưa có tháng này hoặc tháng sau" ngay lúc ghi,
        // phòng khi hai người cùng nhập chỉ số cho một hợp đồng.
        if (!await _repository.AddIfLatestAsync(reading, userId))
        {
            throw new UtilityReadingConflictException("Dữ liệu hợp đồng, phân công, chỉ số hoặc hóa đơn đã thay đổi. Vui lòng tải lại.");
        }

        var created = await _repository.GetByIdAsync(reading.ReadingId, null, null);
        if (created is null)
        {
            throw new InvalidOperationException("Created reading was not found.");
        }
        // Lưu vẫn thành công khi bất thường (cảnh báo không chặn); trả kèm cảnh báo cho client không gọi preview.
        return ConvertToDto(created) with { Warnings = warnings };
    }

    // Rebuild 4.4: kiểm tra giống hệt khi lưu nhưng không ghi gì, để quản lý thấy cảnh báo bất thường TRƯỚC khi lưu
    // (chỉ số đã lưu không sửa được).
    public async Task<UtilityReadingPreviewDto> PreviewAsync(CreateUtilityReadingDto dto, int userId, string role)
    {
        var (reading, warnings, recent) = await PrepareAsync(dto, userId, role);
        return new UtilityReadingPreviewDto(
            reading.ContractId, reading.ReadingMonth,
            reading.ElectricStart, reading.ElectricEnd, reading.ElectricEnd - reading.ElectricStart,
            reading.WaterStart, reading.WaterEnd, reading.WaterEnd - reading.WaterStart,
            recent.Count == 0 ? null : Math.Round(recent.Average(month => month.ElectricUsage), 2, MidpointRounding.AwayFromZero),
            recent.Count == 0 ? null : Math.Round(recent.Average(month => month.WaterUsage), 2, MidpointRounding.AwayFromZero),
            recent.Count, warnings);
    }

    private async Task<(UtilityReadings Reading, List<string> Warnings, List<MonthUsageReadModel> Recent)> PrepareAsync(
        CreateUtilityReadingDto dto, int userId, string role)
    {
        if (role != RoleNames.Manager)
        {
            throw new UtilityReadingAuthorizationException("Only an assigned property manager can record meter readings.");
        }

        var contract = await _repository.GetContractAsync(dto.ContractId);
        if (contract is null)
        {
            throw new UtilityReadingNotFoundException("Contract does not exist.");
        }
        if (!await _repository.HasActiveManagerAssignmentAsync(contract.PropertyId, userId))
        {
            throw new UtilityReadingAuthorizationException("You cannot record readings for this contract.");
        }
        if (contract.Status != ContractStatuses.Active)
        {
            throw new UtilityReadingConflictException("Readings can only be recorded for active contracts.");
        }

        var latest = await _repository.GetLatestAsync(contract.ContractId);
        if (latest is not null && dto.ReadingMonth <= latest.ReadingMonth)
        {
            throw new UtilityReadingConflictException("A reading for this or a later month already exists.");
        }
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), BusinessTimeZone).DateTime);
        var validationError = UtilityReadingPolicy.ValidateAndResolve(contract, latest, dto.ReadingMonth, today,
            out var electricStart, out var waterStart);
        if (validationError is not null)
        {
            throw new UtilityReadingBusinessRuleException(validationError);
        }
        // Legacy start fields cannot replace the first baseline. Later periods always inherit the latest ends.
        if (latest is null && ((dto.ElectricStart.HasValue && dto.ElectricStart != electricStart) ||
            (dto.WaterStart.HasValue && dto.WaterStart != waterStart)))
        {
            throw new UtilityReadingBusinessRuleException("Chỉ số đầu kỳ phải khớp dữ liệu bàn giao đã lưu.");
        }
        if (!UtilityReadingPolicy.IsValidMeterValue(electricStart) || !UtilityReadingPolicy.IsValidMeterValue(waterStart) ||
            !UtilityReadingPolicy.IsValidMeterValue(dto.ElectricEnd) || !UtilityReadingPolicy.IsValidMeterValue(dto.WaterEnd) ||
            dto.ElectricEnd < electricStart || dto.WaterEnd < waterStart)
        {
            throw new UtilityReadingBusinessRuleException("Chỉ số phải không âm, tối đa 16 chữ số nguyên và 2 chữ số thập phân; số cuối kỳ không được nhỏ hơn đầu kỳ.");
        }

        var reading = ConvertToEntity(dto, contract.ContractId, electricStart, waterStart);
        var recent = await _repository.GetRecentUsageAsync(
            contract.ContractId, dto.ReadingMonth, UnusualUsagePolicy.MonthsToAverage);
        var warnings = UnusualUsagePolicy.Warnings(
            reading.ElectricEnd - reading.ElectricStart, reading.WaterEnd - reading.WaterStart, recent);
        return (reading, warnings, recent);
    }

    public async Task<List<UtilityReadingDto>> GetAllAsync(int userId, string role, int? contractId)
    {
        var (customerId, managerId) = GetScope(userId, role);
        var readings = await _repository.GetAllAsync(customerId, managerId, contractId);
        return readings.Select(ConvertToDto).ToList();
    }

    public async Task<UtilityReadingDto?> GetByIdAsync(int id, int userId, string role)
    {
        var (customerId, managerId) = GetScope(userId, role);
        var reading = await _repository.GetByIdAsync(id, customerId, managerId);
        if (reading is null && (customerId.HasValue || managerId.HasValue) && await _repository.ExistsAsync(id))
        {
            throw new UtilityReadingAuthorizationException("You cannot access this reading.");
        }
        if (reading is null)
        {
            return null;
        }
        return ConvertToDto(reading);
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

    private static (int? CustomerId, int? ManagerId) GetScope(int userId, string role)
    {
        var scope = AccessScope.For(userId, role);
        if (scope is null)
        {
            throw new UtilityReadingAuthorizationException("Your role cannot access readings.");
        }
        return scope.Value;
    }

    private static UtilityReadingDto ConvertToDto(UtilityReadingReadModel reading)
    {
        return new UtilityReadingDto(
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
}

public sealed class UtilityReadingNotFoundException(string message) : Exception(message);

public sealed class UtilityReadingAuthorizationException(string message) : Exception(message);

public sealed class UtilityReadingBusinessRuleException(string message) : Exception(message);

public sealed class UtilityReadingConflictException(string message) : Exception(message);
