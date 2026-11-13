using PRN232.Application.ReadModels;
using PRN232.Application.States;

namespace PRN232.Application.Policies;

public static class UtilityReadingPolicy
{
    // UtilityReadings remains numeric(18,2); never silently round a handover when closing a period.
    public static bool IsValidMeterValue(decimal value)
    {
        return value >= 0m && value <= 9_999_999_999_999_999.99m && decimal.Round(value, 2) == value;
    }

    public static string? ValidateAndResolve(
        ReadingContractReadModel contract, LatestReadingReadModel? latest, DateOnly month, DateOnly today,
        out decimal electricStart, out decimal waterStart)
    {
        electricStart = 0m;
        waterStart = 0m;
        if (contract.Status != ContractStatuses.Active)
        {
            return "Readings can only be recorded for active contracts.";
        }
        var firstMonth = new DateOnly(contract.StartDate.Year, contract.StartDate.Month, 1);
        if (month.Day != 1 || month < firstMonth || month > contract.EndDate ||
            month > new DateOnly(today.Year, today.Month, 1))
        {
            return "Reading month must be the first day of a non-future month within the contract term.";
        }
        if (latest is not null)
        {
            if (month != latest.ReadingMonth.AddMonths(1))
            {
                return "Vui lòng chốt các kỳ liên tiếp, không bỏ qua tháng chưa ghi chỉ số.";
            }
            electricStart = latest.ElectricEnd;
            waterStart = latest.WaterEnd;
            return null;
        }
        if (!contract.InitialElectricReading.HasValue || !contract.InitialWaterReading.HasValue ||
            !contract.MeterHandoverDate.HasValue || !IsValidMeterValue(contract.InitialElectricReading.Value) ||
            !IsValidMeterValue(contract.InitialWaterReading.Value) || contract.MeterHandoverDate < contract.StartDate ||
            contract.MeterHandoverDate > contract.EndDate || contract.MeterHandoverDate > today)
        {
            return "Vui lòng ghi nhận chỉ số điện nước bàn giao trước khi chốt kỳ đầu tiên.";
        }
        if (month != firstMonth)
        {
            return "Kỳ chốt đầu tiên phải là tháng bắt đầu hợp đồng.";
        }
        electricStart = contract.InitialElectricReading.Value;
        waterStart = contract.InitialWaterReading.Value;
        return null;
    }
}
