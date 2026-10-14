namespace PRN232.Application.Interfaces;

public interface ICurrentUserService
{
    int? UserId { get; }
    string? Email { get; }
    string? Role { get; }
    string? FullName { get; }
    bool IsAuthenticated { get; }
}
