using PRN232.Application.DTOs;

namespace PRN232.Application.Interfaces;

public interface IBookingService
{
    Task<BookingDto> CreateAsync(CreateBookingDto dto, int customerId);
    Task<List<BookingDto>> GetAllAsync(int userId, string role, string? status);
    Task<BookingDto?> GetByIdAsync(int id, int userId, string role);
    Task<BookingDto> PayAsync(int id, PayBookingDto dto, int customerId);
    Task<BookingDto> CancelAsync(int id, int userId, string role);
}
