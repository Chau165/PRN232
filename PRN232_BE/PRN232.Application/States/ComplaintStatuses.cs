namespace PRN232.Application.States;

public static class ComplaintStatuses
{
    public const string New = "New";
    public const string InProgress = "InProgress";
    public const string Resolved = "Resolved";

    // Khiếu nại chung, không gắn với một dòng cụ thể trên hoá đơn.
    public const string OtherFeeType = "Khác";

    public static bool IsValid(string status) => status is New or InProgress or Resolved;
}
