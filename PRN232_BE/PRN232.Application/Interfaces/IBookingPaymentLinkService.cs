using PRN232.Application.DTOs;

namespace PRN232.Application.Interfaces;

public interface IBookingPaymentLinkService
{
    Task<BookingPaymentLinkDto> CreateAsync(int bookingId, int customerId, CancellationToken cancellationToken);
}
