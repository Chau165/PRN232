namespace PRN232.Application.ReadModels;

public sealed record PropertyOwnerReadModel(
    int PropertyId,
    int OwnerId,
    string OwnerName,
    string OwnerRoleName);
