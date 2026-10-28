namespace PRN232.Application.States;

public static class InvoiceStatuses
{
    public const string Unpaid = "Unpaid";
    public const string Paid = "Paid";
    // Có trong dữ liệu cũ; hoá đơn mới luôn là Unpaid, quá hạn được suy ra từ DueDate.
    public const string Overdue = "Overdue";

    public static bool IsValid(string status) => status is Unpaid or Paid or Overdue;
}

public static class InvoiceItemTypes
{
    public const string Rent = "Rent";
    public const string Electricity = "Electricity";
    public const string Water = "Water";
    public const string Service = "Service";
}
