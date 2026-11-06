namespace PRN232.Application.ReadModels;

public record PropertyServiceRateReadModel(
    int RateId,
    int PropertyId,
    int ServiceId,
    string ServiceName,
    decimal Price,
    string UnitCode,
    string BillingBasis,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo);
