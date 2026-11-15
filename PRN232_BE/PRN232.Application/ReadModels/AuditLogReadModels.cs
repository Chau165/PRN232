namespace PRN232.Application.ReadModels;

public sealed record AuditLogReadModel(
    long AuditLogId, DateTime CreatedAtUtc, int? UserId, string? UserName, string? Role, string Action,
    string HttpMethod, string Path, string EntityType, string? EntityId, int StatusCode, bool Succeeded,
    string? IpAddress, string? CorrelationId, string? OldValues, string? NewValues);
