namespace PRN232.Application.DTOs;

public record InvoiceComplaintDto(
    int ComplaintId,
    int InvoiceId,
    DateOnly InvoiceMonth,
    string RoomCode,
    string PropertyName,
    int CustomerId,
    string CustomerName,
    string FeeType,
    string Content,
    string Status,
    DateTime CreatedAt);

// FeeType là mô tả của một dòng trên hoá đơn (ví dụ "Tiền phòng tháng 10/2026"), hoặc "Khác".
public record CreateInvoiceComplaintDto(int InvoiceId, string FeeType, string Content);

public record UpdateComplaintStatusDto(string Status);
