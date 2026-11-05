using System.ComponentModel.DataAnnotations;

namespace PRN232.Application.DTOs;

public sealed record AssignPropertyManagerRequestDto(
    [param: Range(1, int.MaxValue)] int ManagerId);

public sealed record PropertyManagerAssignmentDto(
    int PropertyId,
    string PropertyName,
    int ManagerId,
    string ManagerName,
    string ManagerEmail,
    bool ManagerIsActive);
