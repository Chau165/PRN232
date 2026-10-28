using PRN232.Application.DTOs;

namespace PRN232.Application.Interfaces;

public interface IBookingService
{
    Task<BookingDto> CreateAsync(CreateBookingDto dto, int customerId);
    Task<BookingDto?> GetByIdAsync(int id, int userId, string role);
    Task<BookingDto> PayAsync(int id, PayBookingDto dto, int customerId);
}
