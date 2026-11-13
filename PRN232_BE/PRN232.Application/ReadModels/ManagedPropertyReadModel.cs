namespace PRN232.Application.ReadModels;

public sealed record ManagedPropertyReadModel(
    int ManagerId,
    PropertySummaryReadModel Property);
