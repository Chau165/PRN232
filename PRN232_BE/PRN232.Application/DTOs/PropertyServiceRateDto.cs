namespace PRN232.Application.DTOs;

public record PropertyServiceRateDto(
    int RateId,
    int PropertyId,
    int ServiceId,
    string ServiceName,
    decimal Price,
    string UnitCode,
    string BillingBasis,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo);
