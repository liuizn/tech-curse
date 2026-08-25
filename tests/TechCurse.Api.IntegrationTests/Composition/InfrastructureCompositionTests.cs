using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using TechCurse.Application.Interfaces;
using TechCurse.Infrastructure;
using TechCurse.Infrastructure.ExternalServices;
using Xunit;

namespace TechCurse.Api.IntegrationTests.Composition;

public class InfrastructureCompositionTests
{
    private sealed class AmbienteFake : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "TechCurse.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static IConfiguration CriarConfiguracao()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UseInMemoryDatabase"] = "true",
                ["ConnectionStrings:RedisCache"] = "localhost:6379,abortConnect=false",
                ["Jwt:Issuer"] = "TesteIssuer",
                ["Jwt:Audience"] = "TesteAudience",
                ["Jwt:SigningKey"] = "ChaveDeTesteSuficientementeLongaParaOJwt123456"
            })
            .Build();
    }

    [Theory]
    [Trait("Category", "Integration")]
    [InlineData("Development")]
    [InlineData("Staging")]
    [InlineData("Testing")]
    public void AddInfrastructure_ForaDeProducao_DeveRegistrarOGatewaySimulado(string ambiente)
    {
        var services = new ServiceCollection();
        var environment = new AmbienteFake { EnvironmentName = ambiente };

        services.AddInfrastructure(CriarConfiguracao(), environment);

        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IPaymentGatewayAdapter));
        descriptor.Should().NotBeNull();
        descriptor!.ImplementationType.Should().Be<SimulatedPaymentGatewayAdapter>();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void AddInfrastructure_EmProducao_DeveAbortarPorFaltaDeGatewayReal()
    {
        var services = new ServiceCollection();
        var environment = new AmbienteFake { EnvironmentName = "Production" };

        var acao = () => services.AddInfrastructure(CriarConfiguracao(), environment);

        acao.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*gateway de pagamento real*");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void AddInfrastructure_EmProducao_NaoDeveRegistrarOGatewaySimulado()
    {
        var services = new ServiceCollection();
        var environment = new AmbienteFake { EnvironmentName = "Production" };

        try
        {
            services.AddInfrastructure(CriarConfiguracao(), environment);
        }
        catch (InvalidOperationException)
        {
        }

        services.Any(d => d.ImplementationType == typeof(SimulatedPaymentGatewayAdapter))
            .Should().BeFalse();
    }
}
