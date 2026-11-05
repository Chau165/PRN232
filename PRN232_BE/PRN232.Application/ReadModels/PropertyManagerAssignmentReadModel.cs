namespace PRN232.Application.ReadModels;

public sealed record PropertyManagerAssignmentReadModel(
    int PropertyId,
    string PropertyName,
    int ManagerId,
    string ManagerName,
    string ManagerEmail,
    bool ManagerIsActive);
