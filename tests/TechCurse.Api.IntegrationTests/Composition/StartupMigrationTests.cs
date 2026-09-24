using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using TechCurse.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TechCurse.Api.IntegrationTests.Composition;

public class StartupMigrationTests
{
    private sealed class BancoInalcancavelFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("UseInMemoryDatabase", "false");
            builder.UseSetting(
                "ConnectionStrings:APITechCurse",
                "Host=127.0.0.1;Port=1;Database=APITechCurse;Username=postgres;Password=SenhaIrrelevante1!;Timeout=1");
            builder.UseSetting("ConnectionStrings:RedisCache", "localhost:6379,abortConnect=false");
            builder.UseSetting("Jwt:Issuer", CustomWebApplicationFactory.JwtIssuer);
            builder.UseSetting("Jwt:Audience", CustomWebApplicationFactory.JwtAudience);
            builder.UseSetting("Jwt:SigningKey", CustomWebApplicationFactory.JwtSigningKey);
            builder.UseSetting("RateLimiting:Enabled", "false");
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Startup_QuandoOBancoRelacionalEstaInalcancavel_NaoDeveSubirAAplicacao()
    {
        using var factory = new BancoInalcancavelFactory();

        var acao = () => factory.CreateClient();

        acao.Should().Throw<Exception>();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Startup_QuandoOProviderNaoEhRelacional_DeveSubirNormalmente()
    {
        await using var factory = new CustomWebApplicationFactory();

        var client = factory.CreateClient();
        var response = await client.GetAsync("/health/live");

        response.IsSuccessStatusCode.Should().BeTrue();
    }
}
