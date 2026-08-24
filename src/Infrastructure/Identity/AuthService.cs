using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using System.Text.Json;
using TechCurse.Application.DTOs;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Exceptions;

namespace TechCurse.Infrastructure.Identity;

public class AuthService : IAuthService
{
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
        var refreshToken = _tokenService.GenerateRefreshToken();

        await _userManager.SetAuthenticationTokenAsync(user, "JWTApp", "RefreshToken", refreshToken);

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

        // 2. Recupera o Refresh Token salvo no banco do Identity
        var savedRefreshToken = await _userManager.GetAuthenticationTokenAsync(user, "JWTApp", "RefreshToken");

        // 3. Valida se o Refresh Token enviado bate com o do banco
        if (savedRefreshToken != input.RefreshToken)
            throw new UnauthorizedException("Refresh Token inválido ou expirado.");

        // 4. Se for válido, gera um novo par de tokens (Rotação de Refresh Token)
        var roles = await _userManager.GetRolesAsync(user);

        // Gera o token JWT através do serviço especializado
        var tokenProcess = _tokenService.GenerateJwtToken(user, roles);
        var refreshToken = _tokenService.GenerateRefreshToken();

        await _userManager.SetAuthenticationTokenAsync(user, "JWTApp", "RefreshToken", refreshToken);

        return new AuthOutputDto(tokenProcess.AccessToken, refreshToken, tokenProcess.ExpiresAt);
    }
}
