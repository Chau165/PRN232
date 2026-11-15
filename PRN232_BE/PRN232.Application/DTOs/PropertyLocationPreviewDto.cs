namespace PRN232.Application.DTOs;
public sealed record PropertyLocationPreviewRequest(string AddressText, string ProvinceCode, string WardCode);
public sealed record PropertyLocationPreviewCandidate(AddressGeocodingCandidate Location, string MatchLevel, IReadOnlyList<string> Reasons);
public sealed record PropertyLocationProviderStatus(string Provider, string Status);
public sealed record PropertyLocationPreviewDto(string Status, string OriginalAddress,
    IReadOnlyList<PropertyLocationPreviewCandidate> Candidates, IReadOnlyList<PropertyLocationProviderStatus> Providers);
