using PRN232.Application.Policies;
using PRN232.Application.ReadModels;
using PRN232.Application.States;
using Xunit;

namespace PRN232.Application.Tests;

public class ContractMeterReadingPolicyTests
{
    private static readonly ReadingContractReadModel Contract = new(1, 9, ContractStatuses.Active,
        new DateOnly(2026, 11, 12), new DateOnly(2027, 5, 12), 1, 1200m, 45m, new DateOnly(2026, 11, 13));
    private static readonly DateOnly Today = new(2026, 12, 15);

    [Fact]
    public void First_month_uses_handover_and_can_be_closed_late_but_cannot_skip_to_december()
    {
        Assert.Null(UtilityReadingPolicy.ValidateAndResolve(Contract, null, new DateOnly(2026, 11, 1), Today, out var electric, out var water));
        Assert.Equal((1200m, 45m), (electric, water));
        Assert.NotNull(UtilityReadingPolicy.ValidateAndResolve(Contract, null, new DateOnly(2026, 12, 1), Today, out _, out _));
    }

    [Fact]
    public void Legacy_contract_can_continue_without_inventing_baseline_and_cannot_skip_a_month()
    {
        var legacy = Contract with { InitialElectricReading = null, InitialWaterReading = null, MeterHandoverDate = null };
        var latest = new LatestReadingReadModel(new DateOnly(2026, 11, 1), 1248m, 49m);
        Assert.Null(UtilityReadingPolicy.ValidateAndResolve(legacy, latest, new DateOnly(2026, 12, 1), Today, out var electric, out var water));
        Assert.Equal((1248m, 49m), (electric, water));
        Assert.NotNull(UtilityReadingPolicy.ValidateAndResolve(legacy, latest, new DateOnly(2027, 1, 1), new DateOnly(2027, 1, 31), out _, out _));
    }

    [Theory]
    [InlineData(2026, 11, 2)]
    [InlineData(2026, 10, 1)]
    [InlineData(2027, 1, 1)]
    [InlineData(2027, 6, 1)]
    public void Invalid_months_are_rejected(int year, int month, int day)
    {
        Assert.NotNull(UtilityReadingPolicy.ValidateAndResolve(Contract, null, new DateOnly(year, month, day), Today, out _, out _));
    }

    [Fact]
    public void Partial_negative_or_out_of_term_baseline_is_rejected()
    {
        foreach (var invalid in new[] {
            Contract with { InitialElectricReading = null }, Contract with { InitialWaterReading = null },
            Contract with { MeterHandoverDate = null }, Contract with { InitialElectricReading = -1m },
            Contract with { InitialWaterReading = -1m }, Contract with { MeterHandoverDate = Contract.StartDate.AddDays(-1) },
            Contract with { MeterHandoverDate = Contract.EndDate.AddDays(1) }, Contract with { Status = ContractStatuses.Terminated } })
        {
            Assert.NotNull(UtilityReadingPolicy.ValidateAndResolve(invalid, null, new DateOnly(2026, 11, 1), Today, out _, out _));
        }
    }

    [Theory]
    [InlineData("0", true)]
    [InlineData("1200.29", true)]
    [InlineData("1200.291", false)]
    [InlineData("10000000000000000", false)]
    [InlineData("-0.01", false)]
    public void Meter_values_fit_the_existing_numeric_18_2_reading_columns(string value, bool expected)
    {
        Assert.Equal(expected, UtilityReadingPolicy.IsValidMeterValue(decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture)));
    }
}
