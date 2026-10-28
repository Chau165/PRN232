using PRN232.Application.DTOs;

namespace PRN232.Application.Interfaces;

public interface IViewingScheduleService
{
    Task<ViewingScheduleDto> CreateAsync(CreateViewingScheduleDto dto, int customerId);
    Task<List<ViewingScheduleDto>> GetAllAsync(int userId, string role, string? status);
    Task<ViewingScheduleDto?> GetByIdAsync(int id, int userId, string role);
    Task<ViewingScheduleDto> ApproveAsync(int id, ApproveViewingScheduleDto dto, int userId, string role);
    Task<ViewingScheduleDto> RejectAsync(int id, int userId, string role);
}
