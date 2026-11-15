namespace PRN232.Application.States;

public static class HandoverKinds
{
    public const string MoveIn = "MoveIn";
    public const string MoveOut = "MoveOut";
}

// Rebuild 4.2: đề nghị chuyển người đứng tên.
public static class ContractTransferStatuses
{
    public const string Pending = "Pending";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
}

// Rebuild 3.5b/3.5c: loại file minh chứng của hợp đồng.
public static class ContractEvidenceKinds
{
    public const string MoveIn = HandoverKinds.MoveIn;
    public const string MoveOut = HandoverKinds.MoveOut;
    public const string SignedContract = "SignedContract";

    public static bool IsValid(string? value) => value is MoveIn or MoveOut or SignedContract;
}

// BLDS 2015 Điều 479: khách bồi thường hư hỏng, mất mát do mình gây ra, KHÔNG bồi thường hao mòn tự nhiên.
public static class EquipmentConditions
{
    public const string Good = "Good";
    public const string Worn = "Worn";
    public const string Damaged = "Damaged";
    public const string Missing = "Missing";

    public static bool IsValid(string? value) => value is Good or Worn or Damaged or Missing;

    // Chỉ hư hỏng hoặc mất mới được tính tiền.
    public static bool IsChargeable(string? value) => value is Damaged or Missing;
}
