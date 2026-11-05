using PRN232.Application.States;

namespace PRN232.Application.Policies;

// Customer chỉ thấy dữ liệu của mình, Manager được lọc theo assignment đang active,
// Admin có phạm vi đọc toàn hệ thống.
// Trả về null với role lạ để mỗi service tự ném lỗi phân quyền của module mình.
public static class AccessScope
{
    public static (int? CustomerId, int? ManagerId)? For(int userId, string role)
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
