using PRN232.Application.DTOs;

namespace PRN232.Application.Interfaces;

public interface IPropertyGeocodingService
{
    Task<AddressCoordinates?> GeocodeAsync(
        string address,
        CancellationToken cancellationToken = default);
}
