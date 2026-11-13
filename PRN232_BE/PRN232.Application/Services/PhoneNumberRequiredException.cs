using PRN232.Application.Interfaces;

namespace PRN232.Application.Services;

/// <summary>
/// Thrown when an action needs the customer's phone number but the account has none yet
/// (typically a Google sign-in account). Controllers map it to 400.
/// </summary>
public sealed class PhoneNumberRequiredException(string message) : Exception(message)
{
    public const string SelfMessage = "Vui lòng cập nhật số điện thoại trong trang cá nhân.";
    public const string CustomerMessage =
        "Khách thuê chưa có số điện thoại. Vui lòng cập nhật số điện thoại trong trang cá nhân.";

    /// <summary>
    /// Throws when the lookup is available and the user has no phone number. The lookup is
    /// optional only so unit tests can construct services without it; the app always provides it.
    /// </summary>
    public static async Task EnsureAsync(IUserContactLookup? lookup, int userId, string message)
    {
        if (lookup is not null && !await lookup.HasPhoneNumberAsync(userId))
        {
            throw new PhoneNumberRequiredException(message);
        }
    }
}
