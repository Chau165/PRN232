using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.States;

namespace PRN232.Application.Services;

public class ReceivablesService(IReceivablesRepository repository, TimeProvider timeProvider) : IReceivablesService
{
    private static readonly TimeZoneInfo BusinessTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    public async Task<ReceivablesReportDto> GetAsync(
        int? propertyId, DateOnly? asOfDate, int userId, string role, CancellationToken cancellationToken)
    {
        if (userId <= 0 || role is not (RoleNames.Admin or RoleNames.Manager))
            throw new ReceivablesForbiddenException();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), BusinessTimeZone).DateTime);
        if (propertyId is <= 0 || (asOfDate.HasValue && asOfDate != today))
            throw new ReceivablesValidationException("PropertyId must be positive. Only today's report in Vietnam time is supported.");
        if (propertyId.HasValue)
        {
            var ownerId = await repository.GetPropertyOwnerAsync(propertyId.Value, cancellationToken);
            if (ownerId is null) throw new ReceivablesNotFoundException();
            if (role == RoleNames.Manager && !await repository.IsManagerAssignedAsync(propertyId.Value, userId, cancellationToken))
                throw new ReceivablesForbiddenException();
        }

        var invoices = (await repository.GetOutstandingAsync(propertyId,
            role == RoleNames.Manager ? userId : null, cancellationToken)).Where(invoice => invoice.Amount > 0).ToList();
        var overdue = invoices.Where(invoice => invoice.DueDate < today).ToList();
        var notYetDue = invoices.Where(invoice => invoice.DueDate >= today).ToList();
        return new ReceivablesReportDto(today, propertyId, invoices.Sum(invoice => invoice.Amount), invoices.Count,
            notYetDue.Sum(invoice => invoice.Amount), notYetDue.Count,
            overdue.Sum(invoice => invoice.Amount), overdue.Count);
    }
}

public sealed class ReceivablesForbiddenException() : Exception("You cannot access receivables for this scope.");
public sealed class ReceivablesNotFoundException() : Exception("Property does not exist.");
public sealed class ReceivablesValidationException(string message) : Exception(message);
