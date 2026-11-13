using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.States;
namespace PRN232.Application.Services;

public class DepositReportService(IDepositReportRepository repository, IRevenueRepository scope, TimeProvider clock) : IDepositReportService
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
    public async Task<DepositReportDto> GetAsync(DateOnly? fromMonth, DateOnly? toMonth, int? propertyId, int userId, string role, CancellationToken cancellationToken)
    {
        if (userId <= 0 || role is not (RoleNames.Admin or RoleNames.Manager)) throw new RevenueForbiddenException();
        var today = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Zone);
        var from = fromMonth ?? new DateOnly(today.Year, 1, 1);
        var to = toMonth ?? new DateOnly(today.Year, today.Month, 1);
        var count = (to.Year - from.Year) * 12 + to.Month - from.Month + 1;
        if (propertyId is <= 0 || from.Year < 2 || from.Day != 1 || to.Day != 1 || count is < 1 or > 24 || to == new DateOnly(9999, 12, 1))
            throw new RevenueValidationException("Use month starts, ascending range of at most 24 months and a positive propertyId.");
        if (propertyId.HasValue)
        {
            if (await scope.GetPropertyOwnerAsync(propertyId.Value, cancellationToken) is null) throw new RevenueNotFoundException();
            if (role == RoleNames.Manager && !await scope.IsManagerAssignedAsync(propertyId.Value, userId, cancellationToken)) throw new RevenueForbiddenException();
        }
        DateTime Utc(DateOnly date) => TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue), Zone);
        var rows = await repository.ReadAsync(propertyId, role == RoleNames.Manager ? userId : null, Utc(from), Utc(to.AddMonths(1)), cancellationToken);
        DateOnly? Month(DepositReportRow row)
        {
            if (row.AtUtc is not { } at) return null;
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(at, DateTimeKind.Utc), Zone);
            return new DateOnly(local.Year, local.Month, 1);
        }
        DepositTotalsDto Totals(IEnumerable<DepositReportRow> values)
        {
            var list = values.ToList();
            return new(list.Sum(r => r.Collected), list.Count(r => r.Collected > 0), list.Sum(r => r.Refunded), list.Count(r => r.Refunded > 0));
        }
        var dated = rows.Where(r => r.AtUtc.HasValue).ToList();
        return new(from, to, propertyId, Totals(dated), Totals(rows.Where(r => r.AtUtc is null)),
            Enumerable.Range(0, count).Select(i => new DepositMonthDto(from.AddMonths(i), Totals(dated.Where(r => Month(r) == from.AddMonths(i))))).ToList(),
            rows.GroupBy(r => new { r.PropertyId, r.PropertyName }).OrderBy(g => g.Key.PropertyId)
                .Select(g => new DepositPropertyDto(g.Key.PropertyId, g.Key.PropertyName, Totals(g.Where(r => r.AtUtc.HasValue)), Totals(g.Where(r => r.AtUtc is null)))).ToList());
    }
}
