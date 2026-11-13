using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.States;

namespace PRN232.Application.Services;

public class RevenueService(IRevenueRepository repository, TimeProvider timeProvider) : IRevenueService
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    public async Task<RevenueReportDto> GetAsync(DateOnly? fromMonth, DateOnly? toMonth, int? propertyId,
        int userId, string role, CancellationToken cancellationToken)
    {
        if (userId <= 0 || role is not (RoleNames.Admin or RoleNames.Manager)) throw new RevenueForbiddenException();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), Zone).DateTime);
        var currentMonth = new DateOnly(today.Year, today.Month, 1);
        var from = fromMonth ?? new DateOnly(today.Year, 1, 1);
        var to = toMonth ?? currentMonth;
        var monthCount = (to.Year - from.Year) * 12 + to.Month - from.Month + 1;
        if (propertyId is <= 0 || from.Day != 1 || to.Day != 1 || monthCount is < 1 or > 24 || to.Year == 9999 && to.Month == 12)
            throw new RevenueValidationException("Months must be month starts in ascending order, at most 24 months; propertyId must be positive.");
        if (propertyId.HasValue)
        {
            var ownerId = await repository.GetPropertyOwnerAsync(propertyId.Value, cancellationToken);
            if (ownerId is null) throw new RevenueNotFoundException();
            if (role == RoleNames.Manager && !await repository.IsManagerAssignedAsync(propertyId.Value, userId, cancellationToken))
                throw new RevenueForbiddenException();
        }
        DateTime Utc(DateOnly date) => TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue), Zone);
        var buckets = await repository.GetBucketsAsync(propertyId, role == RoleNames.Manager ? userId : null,
            Utc(from), Utc(to.AddMonths(1)), cancellationToken);
        var dated = buckets.Where(bucket => bucket.Month.HasValue).ToList();
        var unknown = buckets.Where(bucket => bucket.Month is null).ToList();
        var properties = buckets.GroupBy(bucket => new { bucket.PropertyId, bucket.PropertyName })
            .OrderBy(group => group.Key.PropertyId).ToList();
        var monthly = Enumerable.Range(0, monthCount).Select(offset =>
        {
            var month = from.AddMonths(offset);
            var matches = dated.Where(bucket => bucket.Month == month).ToList();
            return new RevenueMonthDto(month, matches.Sum(bucket => bucket.Amount), matches.Sum(bucket => bucket.PaymentCount));
        }).ToList();
        var byProperty = properties.Select(group => new RevenuePropertyDto(group.Key.PropertyId, group.Key.PropertyName,
            group.Where(bucket => bucket.Month.HasValue).Sum(bucket => bucket.Amount),
            group.Where(bucket => bucket.Month.HasValue).Sum(bucket => bucket.PaymentCount),
            group.Where(bucket => bucket.Month is null).Sum(bucket => bucket.Amount),
            group.Where(bucket => bucket.Month is null).Sum(bucket => bucket.PaymentCount))).ToList();
        var matrix = monthly.SelectMany(month => properties.Select(property =>
        {
            var matches = property.Where(bucket => bucket.Month == month.Month).ToList();
            return new RevenuePropertyMonthDto(month.Month, property.Key.PropertyId, property.Key.PropertyName,
                matches.Sum(bucket => bucket.Amount), matches.Sum(bucket => bucket.PaymentCount));
        })).ToList();
        return new RevenueReportDto(from, to, propertyId, dated.Sum(bucket => bucket.Amount), dated.Sum(bucket => bucket.PaymentCount),
            unknown.Sum(bucket => bucket.Amount), unknown.Sum(bucket => bucket.PaymentCount), monthly, byProperty, matrix);
    }
}

public sealed class RevenueForbiddenException() : Exception("You cannot access revenue for this scope.");
public sealed class RevenueNotFoundException() : Exception("Property does not exist.");
public sealed class RevenueValidationException(string message) : Exception(message);
