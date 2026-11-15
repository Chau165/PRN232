namespace PRN232.Application.DTOs;

public record ServiceDto(
    int ServiceId,
    string Name,
    decimal Price,
    string? DefaultUnitCode = null,
    string? DefaultBillingBasis = null);

public record CreateServiceDto(string Name, decimal Price);

public record UpdateServiceDto(string Name, decimal Price);
