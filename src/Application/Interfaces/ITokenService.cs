using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using TechCurse.Application.DTOs;

namespace TechCurse.Application.Interfaces;

public interface ITokenService
{
    TokenOutputDto GenerateJwtToken(IdentityUser user, IList<string> roles);
    string GenerateRefreshToken();
    ClaimsPrincipal GetPrincipalFromExpiredToken(string token);
}
