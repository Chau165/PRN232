namespace PRN232.Application.Policies;

/// <summary>Display rule for CCCD numbers (A9): only the last 4 digits leave the API.</summary>
public static class CccdMasking
{
    private const int VisibleDigits = 4;
    private static readonly string Mask = new('*', 12);

    public static string MaskNumber(string? cccdNumber)
    {
        var value = cccdNumber?.Trim() ?? string.Empty;
        // A value this short would be fully revealed by "last 4", so show nothing instead.
        return value.Length <= VisibleDigits ? Mask : Mask + value[^VisibleDigits..];
    }
}
