using PRN232.Application.DTOs;

namespace PRN232.Application.Interfaces;

public interface IBookingRefundReviewService
{
    Task<IReadOnlyList<BookingRefundReviewDto>> ListAsync(string? status, int page, int pageSize, CancellationToken cancellationToken);
    Task<BookingRefundReviewDto?> GetAsync(int requestId, CancellationToken cancellationToken);
    Task<BookingRefundReviewDto> ReviewAsync(int requestId, int adminId, ReviewBookingRefundDto input, CancellationToken cancellationToken);
}
