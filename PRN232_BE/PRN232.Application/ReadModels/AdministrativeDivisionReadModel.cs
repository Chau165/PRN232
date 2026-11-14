namespace PRN232.Application.ReadModels;

public sealed record AdministrativeDivisionReadModel(
    string Code,
    string Name,
    string AdministrativeLevel,
    string? ParentProvinceCode);
