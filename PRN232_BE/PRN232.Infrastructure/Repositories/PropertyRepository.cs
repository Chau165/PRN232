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

    public Task<List<PropertySummaryReadModel>> GetAllAsync(int? ownerId)
    {
        return _context.Properties
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
            p.Rooms.Count))
            .FirstOrDefaultAsync();
    }

    public Task<List<PropertyServiceRateReadModel>> GetServiceRatesAsync(
        int propertyId, DateOnly? month, CancellationToken cancellationToken)
    {
        var rates = _context.PropertyServiceRates
            .AsNoTracking()
            .Where(rate => rate.PropertyId == propertyId);
        if (month.HasValue)
        {
            // EffectiveTo is exclusive, matching the invoice billing query.
            var monthStart = month.Value;
            rates = rates.Where(rate => rate.EffectiveFrom <= monthStart &&
                (rate.EffectiveTo == null || rate.EffectiveTo > monthStart));
        }

        return rates.OrderBy(rate => rate.ServiceId)
            .ThenByDescending(rate => rate.EffectiveFrom)
            .ThenBy(rate => rate.RateId)
            .Select(rate => new PropertyServiceRateReadModel(
                rate.RateId, rate.PropertyId, rate.ServiceId, rate.Service!.Name, rate.Price,
                rate.UnitCode, rate.BillingBasis, rate.EffectiveFrom, rate.EffectiveTo))
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ServiceExistsAsync(int serviceId, CancellationToken cancellationToken)
    {
        return _context.Services.AnyAsync(service => service.ServiceId == serviceId, cancellationToken);
    }

    public async Task<bool> AddServiceRateIfNoOverlapAsync(PropertyServiceRates rate, CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        if (_context.Database.IsNpgsql())
        {
            // Serialize writers even when no rate exists yet for this property/service.
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Properties\" WHERE \"PropertyId\" = {rate.PropertyId} FOR UPDATE", cancellationToken);
        }
        var overlaps = await _context.PropertyServiceRates.AnyAsync(existing =>
            existing.PropertyId == rate.PropertyId && existing.ServiceId == rate.ServiceId &&
            (rate.EffectiveTo == null || existing.EffectiveFrom < rate.EffectiveTo) &&
            (existing.EffectiveTo == null || existing.EffectiveTo > rate.EffectiveFrom), cancellationToken);
        if (overlaps)
        {
            return false;
        }

        await _context.PropertyServiceRates.AddAsync(rate, cancellationToken);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is Npgsql.PostgresException { SqlState: "23P01" })
        {
            // Also honor the PostgreSQL exclusion constraint if another writer bypasses this API.
            return false;
        }
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

    public Task<bool> HasRoomsAsync(int propertyId)
    {
        return _context.Rooms.AnyAsync(r => r.PropertyId == propertyId);
    }

    public Task SaveChangesAsync()
    {
        return _context.SaveChangesAsync();
    }
}
