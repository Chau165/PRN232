namespace PRN232.Application.Common;

/// <summary>
/// Shared paging input for list endpoints (A15). Query: <c>page</c> (default 1) and
/// <c>pageSize</c> (default 20, capped at 100). Always build it through <see cref="Create"/>
/// so out-of-range values are clamped instead of rejected.
/// </summary>
public sealed record PageRequest
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    // Keeps (Page - 1) * PageSize inside int range.
    private const int MaxPage = int.MaxValue / MaxPageSize;

    private PageRequest(int page, int pageSize)
    {
        Page = page;
        PageSize = pageSize;
    }

    public int Page { get; }
    public int PageSize { get; }
    public int Skip => (Page - 1) * PageSize;

    public static PageRequest Default { get; } = new(DefaultPage, DefaultPageSize);

    public static PageRequest Create(int? page, int? pageSize)
    {
        var normalizedPage = page is null or < 1 ? DefaultPage : Math.Min(page.Value, MaxPage);
        var normalizedSize = pageSize is null or < 1
            ? DefaultPageSize
            : Math.Min(pageSize.Value, MaxPageSize);
        return new PageRequest(normalizedPage, normalizedSize);
    }
}
