using Microsoft.EntityFrameworkCore;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.States;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;

namespace PRN232.Infrastructure.Repositories;

public sealed class PropertyRoomTypeRepository : IPropertyRoomTypeRepository
{
    private readonly AppDbContext _context;

    public PropertyRoomTypeRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<List<PropertyRoomTypeReadModel>> GetAllByPropertyIdAsync(int propertyId, DateTimeOffset asOfUtc)
    {
        return GetReadQuery(asOfUtc)
            .Where(item => item.PropertyId == propertyId)
            .OrderBy(item => item.RoomTypeName)
            .ThenBy(item => item.RoomTypeId)
            .ToListAsync();
    }

    public Task<PropertyRoomTypeReadModel?> GetByIdAsync(int propertyId, int roomTypeId, DateTimeOffset asOfUtc)
    {
        return GetReadQuery(asOfUtc)
            .Where(item => item.PropertyId == propertyId && item.RoomTypeId == roomTypeId)
            .FirstOrDefaultAsync();
    }

    public Task<PropertyRoomTypes?> GetTrackedByIdAsync(int propertyId, int roomTypeId)
    {
        return _context.PropertyRoomTypes
            .FirstOrDefaultAsync(item => item.PropertyId == propertyId && item.RoomTypeId == roomTypeId);
    }

    public Task<bool> PropertyExistsAsync(int propertyId) =>
        _context.Properties.AnyAsync(property => property.PropertyId == propertyId);

    public Task<bool> HasActiveManagerAssignmentAsync(int propertyId, int managerId) =>
        _context.PropertyManagerAssignments.AnyAsync(assignment =>
            assignment.PropertyId == propertyId &&
            assignment.ManagerId == managerId &&
            assignment.Manager!.IsActive &&
            assignment.Manager.Role!.RoleName == RoleNames.Manager);

    public Task<bool> RoomTypeExistsAsync(int roomTypeId) =>
        _context.RoomTypes.AnyAsync(roomType => roomType.RoomTypeId == roomTypeId);

    public Task<bool> ExistsAsync(int propertyId, int roomTypeId) =>
        _context.PropertyRoomTypes.AnyAsync(item => item.PropertyId == propertyId && item.RoomTypeId == roomTypeId);

    public Task<bool> IsActiveAsync(int propertyId, int roomTypeId) =>
        _context.PropertyRoomTypes.AnyAsync(item =>
            item.PropertyId == propertyId && item.RoomTypeId == roomTypeId && item.IsActive);

    public Task AddAsync(PropertyRoomTypes propertyRoomType) =>
        _context.PropertyRoomTypes.AddAsync(propertyRoomType).AsTask();

    public Task SaveChangesAsync() => _context.SaveChangesAsync();

    private IQueryable<PropertyRoomTypeReadModel> GetReadQuery(DateTimeOffset asOfUtc)
    {
        var rentableRooms = RoomAvailabilityQueries.GetRentableRooms(_context.Rooms, asOfUtc);

        return _context.PropertyRoomTypes
            .AsNoTracking()
            .Select(item => new PropertyRoomTypeReadModel(
                item.PropertyId,
                item.RoomTypeId,
                item.RoomType!.TypeName,
                item.MonthlyRent,
                item.IsActive,
                _context.Rooms.Count(room => room.PropertyId == item.PropertyId && room.RoomTypeId == item.RoomTypeId),
                rentableRooms.Count(room => room.PropertyId == item.PropertyId && room.RoomTypeId == item.RoomTypeId)));
    }
}
