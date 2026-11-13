namespace PRN232.Application.Interfaces;

/// <summary>
/// Contact checks for actions that need to reach the customer. Accounts created through Google
/// sign-in start without a phone number.
/// </summary>
public interface IUserContactLookup
{
    Task<bool> HasPhoneNumberAsync(int userId);
}
