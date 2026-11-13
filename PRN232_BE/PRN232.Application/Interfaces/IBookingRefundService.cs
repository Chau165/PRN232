using PRN232.Application.DTOs;
using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface IBookingRefundService
{
    Task<BookingRefundDto> CreateAsync(int bookingId, int customerId, CreateBookingRefundDto input, CancellationToken cancellationToken);
    Task<BookingRefundDto?> GetAsync(int bookingId, int requestId, int customerId, CancellationToken cancellationToken);
}

public interface IBookingRefundRepository
{
    Task<IReadOnlyList<BookingRefundRequests>> ListForReviewAsync(string? status, int skip, int take, CancellationToken cancellationToken);
    Task<BookingRefundRequests?> GetForReviewAsync(int requestId, CancellationToken cancellationToken);
    Task<BookingRefundRequests> ReviewAsync(int requestId, int adminId, bool approve, decimal? amount, string note, DateTime now, CancellationToken cancellationToken);
    // Checks facts again under the same room/booking locks used by contract creation.
    Task<BookingRefundRequests> CreateAsync(BookingRefundRequests request, CancellationToken cancellationToken);
    Task<BookingRefundRequests?> GetAsync(int bookingId, int requestId, int customerId, CancellationToken cancellationToken);
}

public sealed class BookingRefundNotFoundException() : Exception("Booking or refund request was not found.");
public sealed class BookingRefundForbiddenException() : Exception("You cannot request a refund for this booking.");
public sealed class BookingRefundConflictException(string message) : Exception(message);
public sealed class BookingRefundValidationException(string message) : Exception(message);
