using PRN232.Application.Interfaces;

namespace PRN232.Infrastructure.Security;

public class BCryptPasswordHasher : IPasswordHasher
{
    public string Hash(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    public bool Verify(string password, string passwordHash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // hash trong DB bị sai định dạng (vd. seed tay không qua Hash()) — coi như sai mật khẩu
            return false;
        }
    }
}
