using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using TechCurse.Application.DTOs;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Exceptions;

namespace TechCurse.Infrastructure.Identity;

public class AuthService : IAuthService
{
    /// <summary>Provedor lógico usado nas entradas de <c>AspNetUserTokens</c>.</summary>
    private const string ProvedorDeLogin = "JWTApp";

    /// <summary>
    /// Nome da entrada que guarda o <b>hash</b> do refresh token. O nome permanece
    /// "RefreshToken" por compatibilidade com as linhas já existentes — que, por
    /// serem texto puro, simplesmente deixam de casar e forçam um novo login.
    /// </summary>
    private const string NomeDoRefreshToken = "RefreshToken";

    /// <summary>Nome da entrada que guarda a expiração do refresh token (UTC, ISO-8601).</summary>
    private const string NomeDaExpiracaoDoRefreshToken = "RefreshTokenExpiry";

    private readonly UserManager<IdentityUser> _userManager;
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly ITokenService _tokenService;

    public AuthService(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager, ITokenService tokenService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
    }

    public async Task<bool> RegisterAsync(RegisterInputDto input)
    {
        if (input.Password != input.ConfirmPassword)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                { "Password", new[] { "A senha e a confirmação de senha não coincidem." } }
            });
        }

        var user = new IdentityUser { UserName = input.Name, Email = input.Email };

        var result = await _userManager.CreateAsync(user, input.Password);

        if (result.Succeeded == false)
        {
            Dictionary<string, string[]> errorList = new Dictionary<string, string[]>();

            foreach (var error in result.Errors)
            {
                if (errorList.ContainsKey(error.Code))
                {
                    var existingErrors = errorList[error.Code];
                    var updatedErrors = existingErrors.Concat(new[] { error.Description }).ToArray();
                    errorList[error.Code] = updatedErrors;
                }
                else
                {
                    errorList.Add(error.Code, new[] { error.Description });
                }
            }

            throw new ValidationException(errorList);
        }

        string roleName = input.Role.ToString();

        await _userManager.AddToRoleAsync(user, roleName);

        return true;
    }

    public async Task<AuthOutputDto?> LoginAsync(LoginInputDto input)
    {
        var user = await _userManager.FindByEmailAsync(input.Email);
        if (user == null)
        {
            throw new UnauthorizedException("E-mail ou senha incorretos.");
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, input.Password, lockoutOnFailure: false);
        if (!result.Succeeded)
        {
            throw new UnauthorizedException("Usuário não autenticado.");
        }

        // Busca os papéis (roles) do usuário para embutir no Token JWT
        var roles = await _userManager.GetRolesAsync(user);

        // Gera o token JWT através do serviço especializado
        var tokenProcess = _tokenService.GenerateJwtToken(user, roles);
        var refreshToken = await EmitirRefreshTokenAsync(user);

        return new AuthOutputDto(tokenProcess.AccessToken, refreshToken, tokenProcess.ExpiresAt);
    }

    public async Task<AuthOutputDto?> RefreshAsync(RefreshTokenInputDto input)
    {
        // 1. Extrai o usuário a partir do Access Token expirado
        var principal = _tokenService.GetPrincipalFromExpiredToken(input.AccessToken);
        var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (userId == null)
            throw new UnauthorizedException("Refresh Token inválido ou expirado.");

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            throw new NotFoundException("Usuário não encontrado");

        // 2. Recupera o HASH do Refresh Token salvo no banco do Identity
        var hashPersistido = await _userManager.GetAuthenticationTokenAsync(
            user, ProvedorDeLogin, NomeDoRefreshToken);

        // 3. Valida a expiração própria do Refresh Token. Sem ela, um token vazado
        //    valeria para sempre — a expiração do Access Token não o limita, já que
        //    este fluxo aceita de propósito um Access Token expirado.
        if (!await RefreshTokenEstaVigenteAsync(user))
        {
            await RevogarRefreshTokenAsync(user);

            throw new UnauthorizedException("Refresh Token inválido ou expirado.");
        }

        // 4. Compara em tempo constante o token enviado com o hash do banco
        if (!_tokenService.RefreshTokenMatches(input.RefreshToken, hashPersistido))
        {
            throw new UnauthorizedException("Refresh Token inválido ou expirado.");
        }

        // 5. Se for válido, gera um novo par de tokens (Rotação de Refresh Token)
        var roles = await _userManager.GetRolesAsync(user);

        // Gera o token JWT através do serviço especializado
        var tokenProcess = _tokenService.GenerateJwtToken(user, roles);
        var refreshToken = await EmitirRefreshTokenAsync(user);

        return new AuthOutputDto(tokenProcess.AccessToken, refreshToken, tokenProcess.ExpiresAt);
    }

    /// <summary>
    /// Gera um refresh token, persiste apenas o hash e a expiração, e devolve o valor
    /// em texto puro — que a partir daqui só existe na resposta HTTP ao cliente.
    /// </summary>
    private async Task<string> EmitirRefreshTokenAsync(IdentityUser user)
    {
        var refreshToken = _tokenService.GenerateRefreshToken();
        var expiraEm = _tokenService.GetRefreshTokenExpiration();

        await _userManager.SetAuthenticationTokenAsync(
            user, ProvedorDeLogin, NomeDoRefreshToken, _tokenService.HashRefreshToken(refreshToken));

        await _userManager.SetAuthenticationTokenAsync(
            user, ProvedorDeLogin, NomeDaExpiracaoDoRefreshToken,
            expiraEm.ToString("O", CultureInfo.InvariantCulture));

        return refreshToken;
    }

    /// <summary>
    /// Verifica se existe uma expiração registrada e se ela ainda não passou.
    /// Ausente ou ilegível conta como expirado — é o caso das linhas gravadas pela
    /// versão anterior, que só guardava o token.
    /// </summary>
    private async Task<bool> RefreshTokenEstaVigenteAsync(IdentityUser user)
    {
        var expiracaoPersistida = await _userManager.GetAuthenticationTokenAsync(
            user, ProvedorDeLogin, NomeDaExpiracaoDoRefreshToken);

        if (string.IsNullOrEmpty(expiracaoPersistida))
        {
            return false;
        }

        if (!DateTimeOffset.TryParse(
                expiracaoPersistida,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var expiraEm))
        {
            return false;
        }

        return expiraEm > DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Apaga hash e expiração, invalidando o refresh token do usuário.
    /// </summary>
    private async Task RevogarRefreshTokenAsync(IdentityUser user)
    {
        await _userManager.RemoveAuthenticationTokenAsync(user, ProvedorDeLogin, NomeDoRefreshToken);
        await _userManager.RemoveAuthenticationTokenAsync(user, ProvedorDeLogin, NomeDaExpiracaoDoRefreshToken);
    }
}
