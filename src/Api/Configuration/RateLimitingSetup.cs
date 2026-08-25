using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TechCurse.Api.Configuration;

/// <summary>
/// Rate limiting HTTP, usando o <c>Microsoft.AspNetCore.RateLimiting</c> embutido no framework.
/// </summary>
/// <remarks>
/// O lockout do Identity (5 falhas / 15 min, em <see cref="IdentityAuthenticationSetup"/>)
/// é por usuário: ele protege uma conta específica, mas não impede que um atacante
/// varra milhares de e-mails com uma senha só, nem enumere contas pelo tempo/forma
/// da resposta. Rate limiting por origem fecha essa lacuna antes de a requisição
/// chegar ao Identity.
/// </remarks>
public static class RateLimitingSetup
{
    /// <summary>
    /// Política aplicada aos endpoints de autenticação via
    /// <c>[EnableRateLimiting(RateLimitingSetup.PoliticaAutenticacao)]</c>.
    /// </summary>
    public const string PoliticaAutenticacao = "autenticacao";

    // Padrões escolhidos para serem defensáveis, não agressivos:
    //
    // - Global (200 req / min por origem): um cliente legítimo navegando pela API
    //   (listagens paginadas, detalhes, Swagger) fica uma ordem de grandeza abaixo
    //   disso; um script de scraping ou um loop acidental não fica. O objetivo aqui
    //   é conter abuso e acidente, não moldar tráfego.
    // - Autenticação (10 req / min por IP): login, registro e refresh somados. Um
    //   humano faz 1 ou 2; o refresh acontece a cada 2 horas (vida do access token).
    //   10/min derruba de ~1000 para 10 as senhas testáveis por minuto a partir de
    //   um IP, e mantém margem para NAT corporativo compartilhando um IP de saída.
    private const int LimiteGlobalPadrao = 200;
    private const int JanelaGlobalSegundosPadrao = 60;
    private const int LimiteAutenticacaoPadrao = 10;
    private const int JanelaAutenticacaoSegundosPadrao = 60;

    public static IServiceCollection AddRateLimitingSetup(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        // Desligável por configuração para que a suíte de integração possa martelar
        // os endpoints sem esbarrar no limite. Em produção fica ligado (padrão true).
        var habilitado = configuration.GetValue("RateLimiting:Enabled", true);

        var limiteGlobal = configuration.GetValue("RateLimiting:GlobalPermitLimit", LimiteGlobalPadrao);
        var janelaGlobal = TimeSpan.FromSeconds(
            configuration.GetValue("RateLimiting:GlobalWindowSeconds", JanelaGlobalSegundosPadrao));

        var limiteAutenticacao = configuration.GetValue("RateLimiting:AuthPermitLimit", LimiteAutenticacaoPadrao);
        var janelaAutenticacao = TimeSpan.FromSeconds(
            configuration.GetValue("RateLimiting:AuthWindowSeconds", JanelaAutenticacaoSegundosPadrao));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = (int)HttpStatusCode.TooManyRequests;

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                if (!habilitado)
                {
                    return RateLimitPartition.GetNoLimiter("desabilitado");
                }

                // Requisição autenticada é particionada pelo usuário, não pelo IP:
                // caso contrário toda uma empresa atrás do mesmo NAT dividiria a
                // mesma cota. Anônima cai no IP, que é tudo que se tem.
                return RateLimitPartition.GetFixedWindowLimiter(
                    ObterChaveDeParticao(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limiteGlobal,
                        Window = janelaGlobal,
                        QueueLimit = 0
                    });
            });

            options.AddPolicy(PoliticaAutenticacao, context =>
            {
                if (!habilitado)
                {
                    return RateLimitPartition.GetNoLimiter("desabilitado");
                }

                // Sempre por IP, mesmo que haja token: quem faz força bruta em /login
                // é, por definição, anônimo.
                return RateLimitPartition.GetFixedWindowLimiter(
                    $"auth:{ObterEnderecoRemoto(context)}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limiteAutenticacao,
                        Window = janelaAutenticacao,
                        QueueLimit = 0
                    });
            });

            // O limiter responde antes do ExceptionHandlingMiddleware, então o corpo
            // do 429 precisa ser montado aqui para manter o mesmo formato ProblemDetails
            // do resto da API.
            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                var problemDetails = new ProblemDetails
                {
                    Detail = "Muitas requisições em um curto intervalo. Tente novamente mais tarde.",
                    Instance = context.HttpContext.Request.Path,
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = nameof(HttpStatusCode.TooManyRequests)
                };

                context.HttpContext.Response.ContentType = "application/problem+json";

                await context.HttpContext.Response.WriteAsync(
                    JsonSerializer.Serialize(problemDetails), cancellationToken);
            };
        });

        return services;
    }

    private static string ObterChaveDeParticao(HttpContext context)
    {
        var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return string.IsNullOrEmpty(userId)
            ? $"ip:{ObterEnderecoRemoto(context)}"
            : $"user:{userId}";
    }

    /// <summary>
    /// IP de origem. Cai em "desconhecido" quando não há conexão TCP identificável
    /// (host de teste, socket Unix) — o que agrupa esses casos numa partição só,
    /// em vez de deixá-los sem limite.
    /// </summary>
    private static string ObterEnderecoRemoto(HttpContext context)
        => context.Connection.RemoteIpAddress?.ToString() ?? "desconhecido";
}
