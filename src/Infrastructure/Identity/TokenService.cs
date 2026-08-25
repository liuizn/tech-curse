using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TechCurse.Application.DTOs;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Exceptions;

namespace TechCurse.Infrastructure.Identity;

public class TokenService : ITokenService
{
    /// <summary>Entropia do refresh token. 64 bytes = 512 bits.</summary>
    private const int TamanhoDoRefreshTokenEmBytes = 64;

    /// <summary>Tamanho do digest SHA-256.</summary>
    private const int TamanhoDoHashEmBytes = 32;

    /// <summary>
    /// Validade do refresh token quando <c>Jwt:RefreshTokenDays</c> não é informado.
    /// Sete dias equilibra: o access token dura 2 horas, então o usuário renova a
    /// sessão sem reautenticar durante a semana, e um token roubado tem prazo curto.
    /// </summary>
    private const int DiasDeValidadeDoRefreshTokenPadrao = 7;

    private readonly IConfiguration _configuration;

    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public TokenOutputDto GenerateJwtToken(IdentityUser user, IList<string> roles)
    {
        var tokenHandler = new JwtSecurityTokenHandler();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email!)
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var expiresAt = DateTime.UtcNow.AddHours(2);

        var key = Encoding.ASCII.GetBytes(_configuration["Jwt:SigningKey"]!);
        var credential = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Issuer = _configuration["Jwt:Issuer"],
            Audience = _configuration["Jwt:Audience"],
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt,
            SigningCredentials = credential,
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);

        return new TokenOutputDto(tokenHandler.WriteToken(token), expiresAt);
    }

    public string GenerateRefreshToken()
    {
        // Cria uma string aleatória segura de 64 bytes
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(TamanhoDoRefreshTokenEmBytes));
    }

    /// <summary>
    /// SHA-256 do refresh token, em Base64.
    /// </summary>
    /// <remarks>
    /// SHA-256 puro basta aqui — diferente de uma senha, o refresh token é gerado
    /// pelo servidor com 512 bits de entropia, então não existe espaço de busca a
    /// ser encarecido por um KDF lento. O que se ganha é que um vazamento da tabela
    /// <c>AspNetUserTokens</c> não entrega tokens reutilizáveis.
    /// </remarks>
    public string HashRefreshToken(string refreshToken)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));

        return Convert.ToBase64String(hash);
    }

    public bool RefreshTokenMatches(string refreshToken, string? storedHash)
    {
        if (string.IsNullOrEmpty(refreshToken) || string.IsNullOrEmpty(storedHash))
        {
            return false;
        }

        Span<byte> hashPersistido = stackalloc byte[TamanhoDoHashEmBytes];

        // Hash malformado (truncado, não-Base64, gravado por uma versão anterior em
        // texto puro) é tratado como não-correspondente, sem lançar exceção.
        if (!Convert.TryFromBase64String(storedHash, hashPersistido, out var bytesEscritos)
            || bytesEscritos != TamanhoDoHashEmBytes)
        {
            return false;
        }

        Span<byte> hashRecebido = stackalloc byte[TamanhoDoHashEmBytes];
        SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken), hashRecebido);

        // Comparação em tempo constante: `!=` entre strings sai no primeiro byte
        // divergente, e a diferença de tempo permite descobrir o token byte a byte.
        return CryptographicOperations.FixedTimeEquals(hashRecebido, hashPersistido);
    }

    public DateTime GetRefreshTokenExpiration()
    {
        var dias = _configuration.GetValue("Jwt:RefreshTokenDays", DiasDeValidadeDoRefreshTokenPadrao);

        return DateTime.UtcNow.AddDays(dias);
    }

    public ClaimsPrincipal GetPrincipalFromExpiredToken(string token)
    {
        var key = Encoding.ASCII.GetBytes(_configuration["Jwt:SigningKey"]!);

        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateLifetime = false, // Importante: Ignoramos a expiração para poder ler o token expirado
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = _configuration["Jwt:Issuer"],
            ValidAudience = _configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken securityToken);

        if (securityToken is not JwtSecurityToken jwtSecurityToken ||
            !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
        {
            throw new ForbiddenAccessException("Token inválido");
        }

        return principal;
    }
}
