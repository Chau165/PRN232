using System.Security.Cryptography;
using System.Text;
using PRN232.Application.Interfaces;

namespace PRN232.Infrastructure.Security;

/// <summary>HMAC-SHA256 blind index for CCCD numbers; key comes from IdentityDocuments:CccdHashKey.</summary>
public sealed class HmacCccdHasher : ICccdHasher
{
    public const int MinimumKeyBytes = 32;
    private readonly byte[] _key;

    public HmacCccdHasher(byte[] key)
    {
        if (key.Length < MinimumKeyBytes)
        {
            throw new ArgumentException(
                $"CCCD hash key must be at least {MinimumKeyBytes} bytes.", nameof(key));
        }
        _key = key.ToArray();
    }

    public string Hash(string cccdNumber)
    {
        var normalized = cccdNumber.Trim();
        var digest = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(digest).ToLowerInvariant();
    }
}
