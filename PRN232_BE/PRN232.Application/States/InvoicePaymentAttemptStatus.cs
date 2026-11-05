namespace PRN232.Application.States;

public enum InvoicePaymentAttemptStatus
{
    Completed,
    NoAmountDue,
    Declined,
    NotFound,
    Forbidden,
    Conflict
}
