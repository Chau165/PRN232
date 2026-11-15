using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Policies;

namespace PRN232.Application.Services;

// Rebuild 5.1: trang Admin xem nhật ký thao tác, lọc theo người, loại đối tượng, đối tượng, kết quả, khoảng ngày.
public sealed class AuditLogService(IAuditLogRepository repository) : IAuditLogService
{
    public const int MaxPageSize = 100;
    public const int MaxRangeDays = 366;
    private static readonly TimeZoneInfo BusinessTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    public async Task<AuditLogPageDto> SearchAsync(AuditLogQuery query, CancellationToken cancellationToken)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > MaxPageSize)
        {
            throw new AuditLogQueryException($"Trang bắt đầu từ 1, mỗi trang 1–{MaxPageSize} dòng.");
        }
        if (query.From is { } from && query.To is { } to &&
            (to < from || to.DayNumber - from.DayNumber >= MaxRangeDays))
        {
            throw new AuditLogQueryException($"Khoảng ngày không hợp lệ (đến ngày phải sau từ ngày, tối đa {MaxRangeDays} ngày).");
        }

        var (items, total) = await repository.SearchAsync(query with
            {
                EntityType = string.IsNullOrWhiteSpace(query.EntityType) ? null : query.EntityType.Trim(),
                EntityId = string.IsNullOrWhiteSpace(query.EntityId) ? null : query.EntityId.Trim()
            },
            query.From is { } start ? VietnamDayStartUtc(start) : null,
            query.To is { } end ? VietnamDayStartUtc(end.AddDays(1)) : null,
            cancellationToken);
        return new AuditLogPageDto(items.Select(log => new AuditLogDto(log.AuditLogId, UtcTimestamp.ForResponse(log.CreatedAtUtc),
            log.UserId, log.UserName, log.Role, log.Action, log.HttpMethod, log.Path, log.EntityType, log.EntityId,
            log.StatusCode, log.Succeeded, log.IpAddress, log.CorrelationId, log.OldValues, log.NewValues)).ToList(),
            total, query.Page, query.PageSize);
    }

    private static DateTime VietnamDayStartUtc(DateOnly date) =>
        TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue), BusinessTimeZone);
}

public sealed class AuditLogQueryException(string message) : Exception(message);
