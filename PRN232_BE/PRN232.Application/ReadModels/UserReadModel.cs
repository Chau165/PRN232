namespace PRN232.Application.ReadModels;

public sealed record UserReadModel(
    int UserId,
    string FullName,
    string Email,
    string PhoneNumber,
    int RoleId,
    string RoleName,
    bool IsActive);
