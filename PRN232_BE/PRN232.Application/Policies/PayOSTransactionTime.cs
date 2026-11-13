using System.Globalization;

namespace PRN232.Application.Policies;

public static class PayOSTransactionTime
{
    private static readonly TimeZoneInfo Vietnam = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    public static bool TryGetUtc(string? value, out DateTime utc)
    {
        utc = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        // Explicit application policy for payOS's offset-free example format:
        // interpret it in the business timezone, never in the execution host timezone.
        if (DateTime.TryParseExact(value, new[] { "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm:ss.FFFFFFF" },
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        {
            try { utc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Vietnam); return true; }
            catch (ArgumentException) { return false; }
        }
        if (DateTimeOffset.TryParseExact(value, new[]
            {
                "yyyy-MM-dd'T'HH:mm:sszzz", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz",
                "yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'"
            }, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var offset))
        {
            utc = offset.UtcDateTime;
            return true;
        }
        return false;
    }
}
