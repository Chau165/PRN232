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

    public Task<List<PropertySummaryReadModel>> GetAllAsync(int? ownerId, int? managerId)
    {
        return _context.Properties
            .AsNoTracking()
            .Where(p => ownerId == null || p.OwnerId == ownerId)
            .Where(p => managerId == null || p.ManagerAssignments.Any(assignment =>
                assignment.ManagerId == managerId &&
                assignment.Manager!.IsActive &&
                assignment.Manager.Role!.RoleName == PRN232.Application.States.RoleNames.Manager))
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
            p.Rooms.Count,
            p.PrimaryImageUrl,
            p.Description))
            .ToListAsync();
    }

    public Task<bool> IsManagerAssignedAsync(int propertyId, int managerId)
    {
        return _context.PropertyManagerAssignments
            .AnyAsync(assignment => assignment.PropertyId == propertyId &&
                assignment.ManagerId == managerId &&
                assignment.Manager!.IsActive &&
                assignment.Manager.Role!.RoleName == PRN232.Application.States.RoleNames.Manager);
    }

    public Task<PropertySummaryReadModel?> GetByIdAsync(int id)
    {
        return _context.Properties
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
            p.Rooms.Count,
            p.PrimaryImageUrl,
            p.Description))
            .FirstOrDefaultAsync();
    }

    public Task<Property?> GetTrackedByIdAsync(int id)
    {
        return _context.Properties.FirstOrDefaultAsync(p => p.PropertyId == id);
    }

    public async Task<Property> AddAsync(Property property)
    {
        await _context.Properties.AddAsync(property);
        return property;
    }

    public void Remove(Property property)
    {
        _context.Properties.Remove(property);
    }

    public async Task<bool> HasDependentsAsync(int propertyId)
    {
        return await _context.Rooms.AnyAsync(room => room.PropertyId == propertyId) ||
            await _context.PropertyManagerAssignments.AnyAsync(
                assignment => assignment.PropertyId == propertyId) ||
            await _context.PropertyAmenities.AnyAsync(
                propertyAmenity => propertyAmenity.PropertyId == propertyId) ||
            await _context.PropertyServiceRates.AnyAsync(
                serviceRate => serviceRate.PropertyId == propertyId);
    }

    public Task SaveChangesAsync()
    {
        return _context.SaveChangesAsync();
    }
}
