using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using TechCurse.src.Application.DTOs;

namespace TechCurse.src.Application.Interfaces;

public interface ITokenService
{
    TokenOutputDto GenerateJwtToken(IdentityUser user, IList<string> roles);
    string GenerateRefreshToken();
    ClaimsPrincipal GetPrincipalFromExpiredToken(string token);
}
