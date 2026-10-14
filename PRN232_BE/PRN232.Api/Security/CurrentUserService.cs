using System.Security.Claims;
using PRN232.Application.Interfaces;

namespace PRN232.Api.Security;

public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public int? UserId => int.TryParse(Principal?.FindFirstValue("sub"), out var id) && id > 0 ? id : null;
    public string? Email => Principal?.FindFirstValue("email");
    public string? Role => Principal?.FindFirstValue(ClaimTypes.Role);
    public string? FullName => Principal?.FindFirstValue(ClaimTypes.Name);
    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;
}
