using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using TechCurse.Application.DTOs;

namespace TechCurse.Application.Interfaces;

public interface ITokenService
{
    TokenOutputDto GenerateJwtToken(IdentityUser user, IList<string> roles);

    /// <summary>
    /// Gera o refresh token em texto puro. Este valor é entregue ao cliente e
    /// <b>nunca</b> deve ser persistido — guarde <see cref="HashRefreshToken"/>.
    /// </summary>
    string GenerateRefreshToken();

    /// <summary>
    /// Hash de armazenamento do refresh token.
    /// </summary>
    string HashRefreshToken(string refreshToken);

    /// <summary>
    /// Compara, em tempo constante, um refresh token recebido do cliente com o hash
    /// persistido. Retorna <c>false</c> para hash ausente ou malformado.
    /// </summary>
    bool RefreshTokenMatches(string refreshToken, string? storedHash);

    /// <summary>
    /// Momento (UTC) em que um refresh token emitido agora deixa de valer.
    /// </summary>
    DateTime GetRefreshTokenExpiration();

    ClaimsPrincipal GetPrincipalFromExpiredToken(string token);
}
