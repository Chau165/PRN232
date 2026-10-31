using Microsoft.EntityFrameworkCore;
using PRN232.Application.Interfaces;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;

namespace PRN232.Infrastructure.Repositories;

public class AmenityRepository : IAmenityRepository
{
    private readonly AppDbContext _context;

    public AmenityRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<List<Amenities>> GetAllAsync() =>
        _context.Amenities
            .AsNoTracking()
            .OrderBy(a => a.Name)
            .ToListAsync();

    public Task<Amenities?> GetByIdAsync(int id) =>
        _context.Amenities
            .AsNoTracking()
            .Where(a => a.AmenityId == id)
            .FirstOrDefaultAsync();

    public Task<Amenities?> GetTrackedByIdAsync(int id) =>
        _context.Amenities.FirstOrDefaultAsync(a => a.AmenityId == id);

    public async Task<Amenities> AddAsync(Amenities amenity)
    {
        await _context.Amenities.AddAsync(amenity);
        return amenity;
    }

    public void Remove(Amenities amenity) => _context.Amenities.Remove(amenity);

    public Task SaveChangesAsync() => _context.SaveChangesAsync();
}
