using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace PRN232.Infrastructure.Security;

/// <summary>
/// EF value converter that stores IdentityDocuments.CCCDNumber encrypted with ASP.NET Data
/// Protection (A9). Keys are persisted in the DataProtectionKeys table so they survive redeploys.
/// </summary>
public static class CccdProtection
{
    public const string Purpose = "PRN232.IdentityDocuments.CCCDNumber.v1";

    // Every Data Protection payload starts with the magic header 0x09F0C9F0, i.e. "CfDJ8" in
    // base64url. Anything else is a legacy plaintext row the startup backfill has not reached yet;
    // it is returned as-is so reads keep working until the backfill encrypts it.
    private const string ProtectedPrefix = "CfDJ8";

    public static bool IsProtected(string? value) =>
        value is not null && value.StartsWith(ProtectedPrefix, StringComparison.Ordinal);

    public static ValueConverter<string, string> CreateConverter(IDataProtectionProvider provider)
    {
        var protector = provider.CreateProtector(Purpose);
        return new ValueConverter<string, string>(
            plaintext => protector.Protect(plaintext),
            stored => IsProtected(stored) ? protector.Unprotect(stored) : stored);
    }
}
