using System.Text;
using System.Text.RegularExpressions;

namespace PRN232.Application.Normalizers;

// Some Vietnamese keyboards (Unikey/EVKey in "Unicode to hop" mode) type "ễ" as "ê" plus a separate
// combining tilde. It looks identical but does not match OSM/Photon data or existing rows, so text
// is stored in composed form (NFC) with runs of whitespace collapsed to one space.
public static partial class VietnameseTextNormalizer
{
    public static string Clean(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : Whitespace().Replace(value.Normalize(NormalizationForm.FormC), " ").Trim();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
