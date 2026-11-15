namespace PRN232.Application.Policies;

// Rebuild 3.6b (PLAN.md D3 bước 6, nhóm chốt phương án B). Thứ tự dùng tiền cọc C:
//   1. Bù bồi thường thiết bị B (theo biên bản trả phòng). B > C thì khách nộp thêm B − C.
//   2. Phần còn lại giữ để bù nợ hóa đơn D (chỉ khi khách bỏ đi còn nợ; bình thường khách trả hết trước).
//   3. Phần còn lại nữa: trả trước hạn mà báo dưới 30 ngày thì MẤT; ngược lại HOÀN cho khách.
// Luôn có: bù bồi thường + giữ bù nợ + mất + hoàn = C.
public static class DepositSettlementPolicy
{
    public sealed record Result(decimal DamageCovered, decimal RetainedForDebt, decimal Forfeited, decimal Refund, decimal AmountDue);

    public static Result Calculate(decimal deposit, decimal damageCharges, decimal outstandingDebt, bool earlyWithoutNotice)
    {
        var damageCovered = Math.Min(damageCharges, deposit);
        var amountDue = damageCharges - damageCovered;
        var left = deposit - damageCovered;
        var retained = Math.Min(left, outstandingDebt);
        left -= retained;
        return earlyWithoutNotice
            ? new Result(damageCovered, retained, left, 0m, amountDue)
            : new Result(damageCovered, retained, 0m, left, amountDue);
    }
}
