using Microsoft.EntityFrameworkCore;
using PRN232.Application.Interfaces;
using PRN232.Domain.Entities;
using PRN232.Application.ReadModels;
using PRN232.Infrastructure.Data;

namespace PRN232.Infrastructure.Repositories;

public class PropertyRepository : IPropertyRepository
{
    private readonly AppDbContext _context;

    public PropertyRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<List<PropertySummaryReadModel>> GetAllAsync(int? ownerId) =>
        _context.Properties
            .AsNoTracking()
            .Where(p => ownerId == null || p.OwnerId == ownerId)
            .OrderBy(p => p.PropertyId)
            .Select(p => new PropertySummaryReadModel(
                p.PropertyId,
                p.OwnerId,
                p.Owner!.FullName,
                p.PropertyName,
                p.AddressText,
                p.Ward,
                p.District,
                p.City,
                p.Latitude,
                p.Longitude,
                p.Rooms.Count))
            .ToListAsync();

    public Task<PropertySummaryReadModel?> GetByIdAsync(int id) =>
        _context.Properties
            .AsNoTracking()
            .Where(p => p.PropertyId == id)
            .Select(p => new PropertySummaryReadModel(
                p.PropertyId,
                p.OwnerId,
                p.Owner!.FullName,
                p.PropertyName,
                p.AddressText,
                p.Ward,
                p.District,
                p.City,
                p.Latitude,
                p.Longitude,
                p.Rooms.Count))
            .FirstOrDefaultAsync();

    public Task<Property?> GetTrackedByIdAsync(int id) =>
        _context.Properties.FirstOrDefaultAsync(p => p.PropertyId == id);

    public async Task<Property> AddAsync(Property property)
    {
        await _context.Properties.AddAsync(property);
        return property;
    }

    public void Remove(Property property) => _context.Properties.Remove(property);

    public Task<bool> HasRoomsAsync(int propertyId) =>
        _context.Rooms.AnyAsync(r => r.PropertyId == propertyId);

    public Task SaveChangesAsync() => _context.SaveChangesAsync();
}
