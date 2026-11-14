namespace PRN232.Application.DTOs;

public sealed record AddressGeocodingCandidate(
    AddressCoordinates Coordinates,
    string DisplayAddress,
    string Provider,
    string Precision,
    string? HouseNumber,
    string? StreetName,
    string? WardName,
    string? ProvinceName,
    string? CountryCode,
    IReadOnlyList<AddressAdministrativeArea>? AdministrativeAreas = null,
    IReadOnlyList<AddressAdministrativeArea>? ProvinceAreas = null,
    string? OsmKey = null,
    string? OsmValue = null,
    int ProviderRank = 0);

public sealed record AddressAdministrativeArea(string Source, string Name);

public sealed record PropertyGeocodingQuery(
    string FullAddress,
    string StreetName,
    string? HouseNumber,
    string WardName,
    string ProvinceName);

public sealed record AdministrativeProvinceDto(
    string ProvinceCode,
    string ProvinceName);

public sealed record AdministrativeWardDto(
    string WardCode,
    string WardName);
