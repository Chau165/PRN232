using System.Globalization;
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

// Rebuild 4.4 (PLAN.md D5): cảnh báo (không chặn) khi lượng dùng tháng này lệch nhiều so với trung bình tối đa 3 tháng
// gần nhất. 30% là mốc ngành điện dùng để rà soát/phúc tra chỉ số (tăng hoặc giảm đột biến trên 30%). Bỏ qua chênh
// lệch nhỏ tuyệt đối để phòng ít dùng (vd. nước 2 → 3 m³ = +50%) không bị cảnh báo vô ích.
public static class UnusualUsagePolicy
{
    public const decimal ThresholdRatio = 0.30m;
    public const int MonthsToAverage = 3;
    public const decimal MinElectricDifference = 20m;
    public const decimal MinWaterDifference = 2m;
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    public static List<string> Warnings(decimal electricUsage, decimal waterUsage, IReadOnlyList<MonthUsageReadModel> recent)
    {
        var warnings = new List<string>();
        if (recent.Count == 0)
        {
            return warnings;
        }
        Check("Điện", "kWh", electricUsage, recent.Average(month => month.ElectricUsage), MinElectricDifference, recent.Count, warnings);
        Check("Nước", "m³", waterUsage, recent.Average(month => month.WaterUsage), MinWaterDifference, recent.Count, warnings);
        return warnings;
    }

    private static void Check(string name, string unit, decimal usage, decimal average, decimal minDifference, int months,
        List<string> warnings)
    {
        var difference = usage - average;
        if (Math.Abs(difference) < minDifference || Math.Abs(difference) <= average * ThresholdRatio)
        {
            return;
        }
        var direction = difference > 0 ? "cao" : "thấp";
        var percent = average == 0m ? "" : $" {Math.Round(Math.Abs(difference) / average * 100m, 0, MidpointRounding.AwayFromZero):0}%";
        warnings.Add($"{name} tháng này {Format(usage)} {unit}, {direction} hơn{percent} so với trung bình {months} tháng gần nhất " +
            $"({Format(average)} {unit}). Hãy kiểm tra lại chỉ số trước khi lưu.");
    }

    private static string Format(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero).ToString("0.##", Vietnamese);
}
