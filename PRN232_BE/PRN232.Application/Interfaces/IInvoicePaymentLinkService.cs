using PRN232.Application.DTOs;
namespace PRN232.Application.Interfaces;

public interface IInvoicePaymentLinkService
{
    Task<InvoicePaymentLinkDto> CreateAsync(int invoiceId, int customerId, CancellationToken cancellationToken);
}
