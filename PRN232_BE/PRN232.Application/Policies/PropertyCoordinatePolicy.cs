using PRN232.Application.DTOs;
using PRN232.Application.Services;
namespace PRN232.Application.Policies;
public static class PropertyCoordinatePolicy
{
    // Geographic envelope only, not proof of country/ward membership.
    public static AddressCoordinates Require(decimal? latitude, decimal? longitude)
    {
        if (!latitude.HasValue || !longitude.HasValue)
            throw new PropertyValidationException("Hãy gửi đầy đủ vĩ độ và kinh độ của ghim trên bản đồ.");
        if (!IsLatitudeValid(latitude.Value) || !IsLongitudeValid(longitude.Value))
            throw new PropertyValidationException("Tọa độ ngoài phạm vi hỗ trợ: vĩ độ 6,5–24 và kinh độ 102–117,5.");
        return new(decimal.Round(latitude.Value,6,MidpointRounding.AwayFromZero),decimal.Round(longitude.Value,6,MidpointRounding.AwayFromZero));
    }
    public static bool IsLatitudeValid(decimal value) => value is >= 6.5m and <= 24m;
    public static bool IsLongitudeValid(decimal value) => value is >= 102m and <= 117.5m;
}
