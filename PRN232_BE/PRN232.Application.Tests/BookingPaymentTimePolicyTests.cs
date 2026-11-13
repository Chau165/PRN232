using PRN232.Application.Policies;
using Xunit;
namespace PRN232.Application.Tests;

public class BookingPaymentTimePolicyTests
{
    [Theory]
    [InlineData("2026-11-13T14:57:00Z", "2026-11-13T14:57:19.548336Z", true)]
    [InlineData("2026-11-13T21:57:00+07:00", "2026-11-13T14:57:19.548336Z", true)]
    [InlineData("2026-11-13T14:57:00Z", "2026-11-13T14:57:59.9999999Z", true)]
    [InlineData("2026-11-13T14:57:19Z", "2026-11-13T14:57:19.548336Z", false)]
    [InlineData("2026-11-13T14:56:00Z", "2026-11-13T14:57:19.548336Z", false)]
    [InlineData("2026-11-13T14:57:00Z", "2026-11-13T14:58:00Z", false)]
    [InlineData("2026-11-13T14:57:20Z", "2026-11-13T14:57:19.548336Z", true)]
    public void Creation_lower_bound_only_tolerates_the_same_minute_boundary(string transaction, string created, bool valid)
    {
        Assert.True(PayOSTransactionTime.TryGetUtc(transaction, out var paid));
        Assert.True(PayOSTransactionTime.TryGetUtc(created, out var start));
        Assert.Equal(valid, BookingPaymentTimePolicy.IsAfterCreation(paid, start));
    }
}
