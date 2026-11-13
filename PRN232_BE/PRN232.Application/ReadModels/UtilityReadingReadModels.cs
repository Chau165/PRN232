namespace PRN232.Application.ReadModels;

public sealed record ReadingContractReadModel(
    int ContractId,
    int OwnerId,
    string Status,
    DateOnly StartDate,
    DateOnly EndDate,
    int PropertyId = 0,
    decimal? InitialElectricReading = null,
    decimal? InitialWaterReading = null,
    DateOnly? MeterHandoverDate = null);

public sealed record LatestReadingReadModel(
    DateOnly ReadingMonth,
    decimal ElectricEnd,
    decimal WaterEnd);

public sealed record UtilityReadingReadModel(
    int ReadingId,
    int ContractId,
    string RoomCode,
    string PropertyName,
    string CustomerName,
    DateOnly ReadingMonth,
    decimal ElectricStart,
    decimal ElectricEnd,
    decimal ElectricUsage,
    decimal WaterStart,
    decimal WaterEnd,
    decimal WaterUsage);
