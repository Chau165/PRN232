using PRN232.Application.DTOs;
using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface IAmenityRepository
{
    Task<List<AmenityDto>> GetAllAsync();
    Task<AmenityDto?> GetByIdAsync(int id);
    Task<Amenities?> GetEntityByIdAsync(int id);
    Task<Amenities> AddAsync(Amenities amenity);
    void Remove(Amenities amenity);
    Task SaveChangesAsync();
}
