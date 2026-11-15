namespace PRN232.Application.Settings;

// Trần đơn giá điện nước chủ trọ được nhập (docs/rebuild/PLAN.md, D5). Ghi đè được qua
// appsettings mục "UtilityPriceLimits" mà không cần sửa code khi giá nhà nước thay đổi.
public sealed class UtilityPriceLimits
{
    // Luật cấm chủ trọ thu tổng tiền điện cao hơn hóa đơn EVN. Hệ thống không thấy hóa đơn EVN,
    // nên chặn đơn giá cao hơn giá kWh đắt nhất có thể có: bậc 5 biểu 5 bậc từ 01/11/2026
    // (Quyết định 2892/QĐ-BCT) 3.844đ x VAT 8% (Nghị quyết 204/2025/QH15, đến hết 31/12/2026)
    // = 4.151,52 → 4.152đ. Từ 01/01/2027 nếu VAT trở lại 10% thì là 3.844 x 1,1 = 4.229đ.
    public decimal ElectricityMaxPricePerKwh { get; set; } = 4_152m;

    // Bậc cao nhất hộ gia đình TP.HCM (trên 6m³/người/tháng) 14.400đ + VAT 5% (720đ)
    // + phí bảo vệ môi trường 10% (1.440đ, Nghị định 346/2025/NĐ-CP) = 16.560đ.
    // CHƯA xác nhận được biểu giá chính thức năm 2026 của TP.HCM; tỉnh khác có giá khác.
    public decimal WaterMaxPricePerM3 { get; set; } = 16_560m;

    // Phí cố định hằng tháng: theo phòng (wifi, rác), theo người (nước khoán), theo xe (gửi xe).
    public decimal MonthlyFeeMax { get; set; } = 1_000_000m;
}
