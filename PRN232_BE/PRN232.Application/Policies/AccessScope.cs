using PRN232.Application.States;

namespace PRN232.Application.Policies;

// Phạm vi dữ liệu theo role, dùng chung cho booking, lịch xem và hợp đồng:
// Customer chỉ thấy của mình, Manager thấy khu trọ mình sở hữu, Admin thấy tất cả.
// Trả về null với role lạ để mỗi service tự ném lỗi phân quyền của module mình.
public static class AccessScope
{
    public static (int? CustomerId, int? OwnerId)? For(int userId, string role)
    {
        return role switch
        {
            RoleNames.Customer => (userId, null),
            RoleNames.Manager => (null, userId),
            RoleNames.Admin => (null, null),
            _ => null
        };
    }
}
