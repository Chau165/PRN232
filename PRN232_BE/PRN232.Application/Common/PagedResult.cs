namespace PRN232.Application.Common;

/// <summary>Shared paged response: <c>{ items, page, pageSize, totalCount }</c> (A15).</summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public PagedResult<TResult> Map<TResult>(Func<T, TResult> selector) =>
        new(Items.Select(selector).ToArray(), Page, PageSize, TotalCount);
}
