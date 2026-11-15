namespace PRN232.Application.DTOs;

// Một dòng nhật ký cần ghi (Api tạo từ request; service tạo cho nghiệp vụ quan trọng ở lớp 2).
public sealed record AuditLogEntry(
    int? UserId,
    string? Role,
    string Action,
    string HttpMethod,
    string Path,
    string EntityType,
    string? EntityId,
    int StatusCode,
    bool Succeeded,
    string? IpAddress,
    string? CorrelationId,
    string? OldValues = null,
    string? NewValues = null);

// Bộ lọc trang Admin. From/To là ngày theo giờ Việt Nam (bao gồm cả hai đầu).
public sealed record AuditLogQuery(
    int? UserId = null,
    string? EntityType = null,
    string? EntityId = null,
    bool? Succeeded = null,
    DateOnly? From = null,
    DateOnly? To = null,
    int Page = 1,
    int PageSize = 50);

public sealed record AuditLogDto(
    long AuditLogId,
    DateTime CreatedAtUtc,
    int? UserId,
    string? UserName,
    string? Role,
    string Action,
    string HttpMethod,
    string Path,
    string EntityType,
    string? EntityId,
    int StatusCode,
    bool Succeeded,
    string? IpAddress,
    string? CorrelationId,
    string? OldValues,
    string? NewValues);

public sealed record AuditLogPageDto(IReadOnlyList<AuditLogDto> Items, int TotalCount, int Page, int PageSize);
