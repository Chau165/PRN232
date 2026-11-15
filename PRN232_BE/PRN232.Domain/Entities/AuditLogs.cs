namespace PRN232.Domain.Entities;

// Rebuild 5.1 (PLAN.md D7): nhật ký thao tác — ai làm gì, lúc nào, kết quả ra sao.
// Lớp 1 (action filter) ghi MỌI request thay đổi dữ liệu, không lưu body (tránh lộ mật khẩu, CCCD, tài khoản
// ngân hàng). Lớp 2 (5.2) ghi thêm OldValues/NewValues cho nghiệp vụ quan trọng.
// Không có khóa ngoại tới Users: nhật ký phải còn nguyên kể cả khi tài khoản bị xóa.
public class AuditLogs
{
    public long AuditLogId { get; set; }
    public int? UserId { get; set; }
    public string? Role { get; set; }
    public string Action { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public int StatusCode { get; set; }
    public bool Succeeded { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? IpAddress { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
