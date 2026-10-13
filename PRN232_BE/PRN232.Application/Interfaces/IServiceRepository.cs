using PRN232.Application.DTOs;
using Entities = PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface IServiceRepository
{
    Task<List<ServiceDto>> GetAllAsync();
    Task<ServiceDto?> GetByIdAsync(int id);
    Task<Entities.Service?> GetEntityByIdAsync(int id);
    Task<Entities.Service> AddAsync(Entities.Service service);
    void Remove(Entities.Service service);
    Task SaveChangesAsync();
}
