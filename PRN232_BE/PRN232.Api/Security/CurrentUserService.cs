using System.Security.Claims;
using PRN232.Application.Interfaces;

namespace PRN232.Api.Security;

public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Principal
    {
        get
        {
            return _httpContextAccessor.HttpContext?.User;
        }
    }
    public int? UserId
    {
        get
        {
            if (int.TryParse(Principal?.FindFirstValue("sub"), out var id) && id > 0)
            {
                return id;
            }
            return null;
        }
    }
    public string? Email
    {
        get
        {
            return Principal?.FindFirstValue("email");
        }
    }
    public string? Role
    {
        get
        {
            return Principal?.FindFirstValue(ClaimTypes.Role);
        }
    }
    public string? FullName
    {
        get
        {
            return Principal?.FindFirstValue(ClaimTypes.Name);
        }
    }
    public bool IsAuthenticated
    {
        get
        {
            return Principal?.Identity?.IsAuthenticated == true;
        }
    }
}
