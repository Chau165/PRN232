using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;

namespace PRN232.Infrastructure.Repositories;

// Rebuild 5.1: mỗi lần ghi dùng một scope (DbContext) mới, tách hẳn khỏi DbContext của request.
public sealed class AuditLogWriter(IServiceScopeFactory scopes, TimeProvider clock) : IAuditLogWriter
{
    public async Task WriteAsync(AuditLogEntry entry, CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.AuditLogs.Add(new AuditLogs
        {
            UserId = entry.UserId,
            Role = Truncate(entry.Role, 50),
            Action = Truncate(entry.Action, 150)!,
            HttpMethod = Truncate(entry.HttpMethod, 10)!,
            Path = Truncate(entry.Path, 300)!,
            EntityType = Truncate(entry.EntityType, 100)!,
            EntityId = Truncate(entry.EntityId, 50),
            StatusCode = entry.StatusCode,
            Succeeded = entry.Succeeded,
            OldValues = entry.OldValues,
            NewValues = entry.NewValues,
            IpAddress = Truncate(entry.IpAddress, 64),
            CorrelationId = Truncate(entry.CorrelationId, 100),
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime
        });
        await context.SaveChangesAsync(cancellationToken);
    }

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];
}

public sealed class AuditLogRepository(AppDbContext context) : IAuditLogRepository
{
    public async Task<(List<AuditLogReadModel> Items, int TotalCount)> SearchAsync(AuditLogQuery query, DateTime? fromUtc,
        DateTime? toUtc, CancellationToken cancellationToken)
    {
        var logs = context.AuditLogs.AsNoTracking()
            .Where(log => query.UserId == null || log.UserId == query.UserId)
            .Where(log => query.EntityType == null || log.EntityType == query.EntityType)
            .Where(log => query.EntityId == null || log.EntityId == query.EntityId)
            .Where(log => query.Succeeded == null || log.Succeeded == query.Succeeded)
            .Where(log => fromUtc == null || log.CreatedAtUtc >= fromUtc)
            .Where(log => toUtc == null || log.CreatedAtUtc < toUtc);
        var total = await logs.CountAsync(cancellationToken);
        var rows = await logs
            .OrderByDescending(log => log.CreatedAtUtc).ThenByDescending(log => log.AuditLogId)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(log => new
            {
                log,
                UserName = context.Users.Where(user => user.UserId == log.UserId).Select(user => user.FullName).FirstOrDefault()
            })
            .ToListAsync(cancellationToken);
        return (rows.Select(row => new AuditLogReadModel(row.log.AuditLogId, row.log.CreatedAtUtc,
            row.log.UserId, row.UserName, row.log.Role, row.log.Action, row.log.HttpMethod, row.log.Path,
            row.log.EntityType, row.log.EntityId, row.log.StatusCode, row.log.Succeeded, row.log.IpAddress,
            row.log.CorrelationId, row.log.OldValues, row.log.NewValues)).ToList(), total);
    }
}
