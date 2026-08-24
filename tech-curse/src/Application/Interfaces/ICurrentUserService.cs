using TechCurse.src.Domain.Enums;

namespace TechCurse.src.Application.Interfaces;

public interface ICurrentUserService
{
    string? GetUserId();
    string? GetUserEmail();
    bool IsInRole(UserRole roleName);
}
