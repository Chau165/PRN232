namespace PRN232.Application.DTOs;

public record IdentityDocumentDto(
    int IdentityDocumentId,
    int CustomerId,
    string CustomerName,
    string CCCDNumber,
    string Status,
    int? ReviewedBy,
    string? ReviewedByName);

// Customer tự nộp số CCCD của mình — CustomerId lấy từ claim "sub", không tin client
// khai báo CustomerId của người khác.
public record SubmitIdentityDocumentDto(string CCCDNumber);

// Admin/Manager duyệt: Status chỉ nhận "Approved" hoặc "Rejected".
public record ReviewIdentityDocumentDto(string Status);
