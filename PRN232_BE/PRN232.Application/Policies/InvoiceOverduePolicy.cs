using System.Linq.Expressions;
using PRN232.Application.States;
using PRN232.Application.Validation;
using PRN232.Domain.Entities;

namespace PRN232.Application.Policies;

/// <summary>
/// B5: an unpaid invoice (stored Unpaid, or a legacy stored Overdue) reads as Overdue exactly when
/// its DueDate is before today in Vietnam time, and as Unpaid otherwise. This is the rule the
/// receivables report already uses. The stored status is never rewritten, so every payment path
/// (PayOS, cash, webhook) keeps treating the invoice as payable exactly as before.
/// </summary>
public static class InvoiceOverduePolicy
{
    /// <summary>"Today" in Asia/Ho_Chi_Minh, the same clock used for bookings and contracts.</summary>
    public static DateOnly Today(TimeProvider clock) => BookingRules.Today(clock);

    public static bool IsOpen(string storedStatus) =>
        storedStatus is InvoiceStatuses.Unpaid or InvoiceStatuses.Overdue;

    public static string EffectiveStatus(string storedStatus, DateOnly dueDate, DateOnly today) =>
        IsOpen(storedStatus)
            ? dueDate < today ? InvoiceStatuses.Overdue : InvoiceStatuses.Unpaid
            : storedStatus;

    /// <summary>SQL-translatable filter for status=Overdue.</summary>
    public static Expression<Func<Invoices, bool>> IsOverdue(DateOnly today) =>
        invoice => (invoice.Status == InvoiceStatuses.Unpaid || invoice.Status == InvoiceStatuses.Overdue) &&
            invoice.DueDate < today;

    /// <summary>SQL-translatable filter for status=Unpaid (excludes invoices that read as Overdue).</summary>
    public static Expression<Func<Invoices, bool>> IsUnpaidNotOverdue(DateOnly today) =>
        invoice => (invoice.Status == InvoiceStatuses.Unpaid || invoice.Status == InvoiceStatuses.Overdue) &&
            invoice.DueDate >= today;
}
