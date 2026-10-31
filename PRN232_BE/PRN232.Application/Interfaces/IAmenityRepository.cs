using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface IAmenityRepository
{
    Task<List<Amenities>> GetAllAsync();
    Task<Amenities?> GetByIdAsync(int id);
    Task<Amenities?> GetTrackedByIdAsync(int id);
    Task<Amenities> AddAsync(Amenities amenity);
    void Remove(Amenities amenity);
    Task SaveChangesAsync();
}
