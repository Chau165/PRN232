namespace PRN232.Application.Interfaces;

// Rebuild 5.2 (PLAN.md D7, lớp 2): service ghi giá trị TRƯỚC/SAU của nghiệp vụ quan trọng vào đây; bộ lọc nhật ký
// (lớp 1) lưu chúng vào cột OldValues/NewValues của chính dòng nhật ký của request đó.
// Chỉ gọi SAU KHI ghi DB thành công, và chỉ truyền các trường nghiệp vụ được chọn sẵn — không bao giờ truyền
// mật khẩu, CCCD, token hay số tài khoản ngân hàng.
public interface IAuditTrail
{
    void Record(object? oldValues, object? newValues);
}
