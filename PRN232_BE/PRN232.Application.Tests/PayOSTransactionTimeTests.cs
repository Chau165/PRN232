using PRN232.Application.Policies;
using Xunit;

namespace PRN232.Application.Tests;

public class PayOSTransactionTimeTests
{
    [Theory]
    [InlineData("2026-11-01 00:00:00")]
    [InlineData("2026-11-01T00:00:00+07:00")]
    [InlineData("2026-10-31T17:00:00Z")]
    public void Business_local_and_explicit_offsets_resolve_to_same_utc_month_boundary(string input)
    {
        Assert.True(PayOSTransactionTime.TryGetUtc(input, out var utc));
        Assert.Equal(new DateTime(2026, 10, 31, 17, 0, 0, DateTimeKind.Utc), utc);
        Assert.Equal(DateTimeKind.Utc, utc.Kind);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2026-11-01")]
    [InlineData("11/01/2026 10:00:00")]
    [InlineData("not-a-date")]
    public void Ambiguous_or_missing_time_is_rejected(string? input) => Assert.False(PayOSTransactionTime.TryGetUtc(input, out _));
}
