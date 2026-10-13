using PRN232.Application.DTOs;
using PRN232.Domain.Entities;

namespace PRN232.Application.Interfaces;

public interface IPropertyRepository
{
    Task<List<PropertyDto>> GetAllAsync(int? ownerId);
    Task<PropertyDto?> GetByIdAsync(int id);
    Task<Property?> GetEntityByIdAsync(int id);
    Task<Property> AddAsync(Property property);
    void Remove(Property property);
    Task<bool> HasRoomsAsync(int propertyId);
    Task SaveChangesAsync();
}
