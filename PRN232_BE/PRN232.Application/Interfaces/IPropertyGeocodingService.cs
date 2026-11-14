using PRN232.Application.DTOs;

namespace PRN232.Application.Interfaces;

public interface IPropertyGeocodingService
{
    Task<IReadOnlyList<AddressGeocodingCandidate>> FindNominatimCandidatesAsync(
        PropertyGeocodingQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AddressGeocodingCandidate>> FindPhotonCandidatesAsync(
        PropertyGeocodingQuery query,
        CancellationToken cancellationToken = default);
}
