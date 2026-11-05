namespace PRN232.Application.DTOs;

public sealed record ContractBillingTimelineDto(
    ContractDto Contract,
    IReadOnlyList<InvoiceDto> IssuedInvoices,
    IReadOnlyList<PlannedBillingPeriodDto> PlannedBillingPeriods,
    bool HasMorePlannedPeriods);

public sealed record PlannedBillingPeriodDto(
    DateOnly InvoiceMonth,
    decimal MonthlyRent,
    string DisplayStatus,
    decimal? GrossTotal,
    decimal? PrepaidRentApplied,
    decimal? NetPayable,
    string UtilitiesStatus,
    string? Note);
