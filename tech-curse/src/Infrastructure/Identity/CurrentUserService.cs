using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using TechCurse.src.Application.Interfaces;
using TechCurse.src.Domain.Enums;

namespace TechCurse.src.Infrastructure.Identity;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? GetUserId()
    {
        var user = _httpContextAccessor.HttpContext?.User;

        return user?.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    public string? GetUserEmail()
    {
        var user = _httpContextAccessor.HttpContext?.User;

        return user?.FindFirstValue(ClaimTypes.Email);
    }

    public bool IsInRole(UserRole roleName)
        => _httpContextAccessor.HttpContext?.User?.IsInRole(roleName.ToString()) ?? false;
}
