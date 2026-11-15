namespace PRN232.Domain.Entities;

// Rebuild 3.5b/3.5c: ảnh/PDF minh chứng của hợp đồng — ảnh nhận phòng, ảnh trả phòng, bản hợp đồng đã ký.
// Lưu trong bucket PRIVATE (chứa chữ ký, CCCD); chỉ đọc qua link ký tạm. Khách và quản lý cùng upload, không ai
// duyệt ảnh của bên kia; Admin chỉ ẩn được (kèm lý do), không xóa.
public class ContractEvidenceFiles
{
    public int ContractEvidenceFileId { get; set; }
    public int ContractId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string ObjectPath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string FileName { get; set; } = string.Empty;
    public int UploadedById { get; set; }
    public DateTime UploadedAtUtc { get; set; }
    public DateTime? HiddenAtUtc { get; set; }
    public int? HiddenById { get; set; }
    public string? HiddenReason { get; set; }

    public Contracts? Contract { get; set; }
    public Users? UploadedBy { get; set; }
    public Users? HiddenBy { get; set; }
}
