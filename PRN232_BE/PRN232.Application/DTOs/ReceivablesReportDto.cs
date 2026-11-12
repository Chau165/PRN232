namespace PRN232.Application.DTOs;

public record ReceivablesReportDto(
    DateOnly AsOfDate,
    int? PropertyId,
    decimal OutstandingAmount,
    int OutstandingInvoiceCount,
    decimal NotYetDueAmount,
    int NotYetDueInvoiceCount,
    decimal OverdueAmount,
    int OverdueInvoiceCount);
