namespace PRN232.Application.States;

public static class IdentityDocumentStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";

    public static readonly string[] ReviewTargets = { Approved, Rejected };

    // Chỉ duyệt được khi đang Pending — đã Approved/Rejected rồi thì không đổi nữa
    // (muốn nộp lại thì phải submit hồ sơ mới).
    public static bool IsAllowedTransition(string current, string target) =>
        current == Pending && ReviewTargets.Contains(target);
}
