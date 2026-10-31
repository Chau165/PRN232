namespace PRN232.Application.ReadModels;

public sealed record PropertyAvailabilityReadModel(
    int PropertyId,
    int AvailableRoomCount,
    decimal? StartingPrice);
