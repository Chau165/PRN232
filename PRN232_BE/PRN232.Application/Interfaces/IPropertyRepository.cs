using PRN232.Application.ReadModels;

namespace PRN232.Application.Interfaces;

public interface IPropertyRepository
{
    Task<List<PropertySummaryReadModel>> GetAllAsync(int? ownerId);
    Task<PropertySummaryReadModel?> GetByIdAsync(int id);
    Task<PRN232.Domain.Entities.Property?> GetTrackedByIdAsync(int id);
    Task<PRN232.Domain.Entities.Property> AddAsync(PRN232.Domain.Entities.Property property);
    void Remove(PRN232.Domain.Entities.Property property);
    Task<bool> HasRoomsAsync(int propertyId);
    Task SaveChangesAsync();
}
