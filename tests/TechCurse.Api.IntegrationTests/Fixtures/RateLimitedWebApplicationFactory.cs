using Microsoft.AspNetCore.Hosting;

namespace TechCurse.Api.IntegrationTests.Fixtures;

/// <summary>
/// Host de teste com o rate limiting <b>ligado</b> e com a cota de autenticação
/// reduzida a duas requisições por janela.
/// </summary>
/// <remarks>
/// A <see cref="CustomWebApplicationFactory"/> desliga o limiter para o resto da
/// suíte: todos os testes chegam pelo mesmo IP (nulo, no TestServer) e cairiam na
/// mesma partição, transformando o volume da suíte em 429 aleatório. Aqui o limite
/// é encolhido de propósito para que o comportamento seja exercitado em poucas
/// requisições, sem <c>Thread.Sleep</c> nem dependência da janela real de 60s.
/// </remarks>
public class RateLimitedWebApplicationFactory : CustomWebApplicationFactory
{
    /// <summary>Requisições permitidas por janela nos endpoints de autenticação.</summary>
    public const int LimiteDeAutenticacao = 2;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.UseSetting("RateLimiting:Enabled", "true");
        builder.UseSetting("RateLimiting:AuthPermitLimit", LimiteDeAutenticacao.ToString());
        builder.UseSetting("RateLimiting:AuthWindowSeconds", "60");
    }
}
