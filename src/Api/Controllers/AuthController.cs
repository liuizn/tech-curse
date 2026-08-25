using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;
using TechCurse.Api.Configuration;
using TechCurse.Application.DTOs;
using TechCurse.Application.Interfaces;

namespace TechCurse.Api.Controllers;

[ApiController]
[Route("tech-curse/[controller]")]
[Consumes("application/json")]
[Produces("application/json")]
[Tags("Auth")]
// Política mais restritiva que a global: estes são os únicos endpoints públicos
// que aceitam credenciais, e portanto os únicos alvos de força bruta.
[EnableRateLimiting(RateLimitingSetup.PoliticaAutenticacao)]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authAppService)
    {
        _authService = authAppService;
    }

    [HttpPost("register")]
    [SwaggerOperation(
        Summary = "Registra um novo usuário no sistema.",
        Description = "**Acesso:** Público."
    )]
    [SwaggerResponse(StatusCodes.Status201Created, "Usuário registrado com sucesso.")]
    [SwaggerResponse(StatusCodes.Status409Conflict, "Conflito. O e-mail informado já está em uso.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status422UnprocessableEntity, "Erro de validação nos campos enviados.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status429TooManyRequests, "Limite de requisições de autenticação excedido.", typeof(ProblemDetails))]
    public async Task<IActionResult> Register([FromBody] RegisterInputDto input)
    {
        var actionResult = await _authService.RegisterAsync(input);

        return StatusCode(201, "Usuário registrado com sucesso.");
    }

    [HttpPost("login")]
    [SwaggerOperation(
        Summary = "Realiza o login de um usuário e retorna o Token JWT.",
        Description = "**Acesso:** Público."
    )]
    [SwaggerResponse(StatusCodes.Status200OK, "Autenticação bem-sucedida. Retorna o Token JWT.", typeof(AuthOutputDto))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Credenciais inválidas.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Usuário não autenticado ou inativo.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status429TooManyRequests, "Limite de tentativas de login excedido.", typeof(ProblemDetails))]
    public async Task<IActionResult> Login([FromBody] LoginInputDto input)
    {
        var authResult = await _authService.LoginAsync(input);

        return Ok(authResult);
    }

    [HttpPost("refresh")]
    [SwaggerOperation(
        Summary = "Gera um novo Token JWT a partir de um Refresh Token válido.",
        Description = "**Acesso:** Público."
    )]
    [SwaggerResponse(StatusCodes.Status200OK, "Token atualizado com sucesso.", typeof(TokenOutputDto))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Refresh token expirado ou inválido.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status429TooManyRequests, "Limite de requisições de autenticação excedido.", typeof(ProblemDetails))]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenInputDto input)
    {
        var refreshResult = await _authService.RefreshAsync(input);

        return Ok(refreshResult);
    }
}
