namespace PRN232.Application.Policies;

// OD07: a deposited booking holds the room for 7 days after the deposit is paid. When that time
// is over and no contract was signed, staff may release the room and the deposit is kept.
public static class DepositHoldPolicy
{
    public static readonly TimeSpan HoldPeriod = TimeSpan.FromDays(7);

    // Null when the deposit time is unknown (legacy payments without PaidAtUtc).
    public static DateTime? HoldUntilUtc(DateTime? depositPaidAtUtc) => depositPaidAtUtc?.Add(HoldPeriod);

    // Legacy deposits without a paid time were taken long ago, so their hold counts as over.
    public static bool IsHoldOver(DateTime? depositPaidAtUtc, DateTime nowUtc) =>
        depositPaidAtUtc is null || depositPaidAtUtc.Value.Add(HoldPeriod) <= nowUtc;
}
