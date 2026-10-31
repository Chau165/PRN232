using Entities = PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface IServiceRepository
{
    Task<List<Entities.Service>> GetAllAsync();
    Task<Entities.Service?> GetByIdAsync(int id);
    Task<Entities.Service?> GetTrackedByIdAsync(int id);
    Task<Entities.Service> AddAsync(Entities.Service service);
    void Remove(Entities.Service service);
    Task SaveChangesAsync();
}
