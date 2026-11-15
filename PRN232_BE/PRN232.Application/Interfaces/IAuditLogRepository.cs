using PRN232.Application.DTOs;
using PRN232.Application.ReadModels;

namespace PRN232.Application.Interfaces;

// Ghi nhật ký bằng DbContext RIÊNG: không bao giờ vô tình lưu kèm thay đổi dở dang của request đang chạy.
public interface IAuditLogWriter
{
    Task WriteAsync(AuditLogEntry entry, CancellationToken cancellationToken);
}

public interface IAuditLogRepository
{
    // fromUtc bao gồm, toUtc không bao gồm.
    Task<(List<AuditLogReadModel> Items, int TotalCount)> SearchAsync(AuditLogQuery query, DateTime? fromUtc, DateTime? toUtc,
        CancellationToken cancellationToken);
}

public interface IAuditLogService
{
    Task<AuditLogPageDto> SearchAsync(AuditLogQuery query, CancellationToken cancellationToken);
}
