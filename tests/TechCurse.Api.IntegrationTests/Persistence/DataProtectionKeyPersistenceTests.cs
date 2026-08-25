using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TechCurse.Api.IntegrationTests.Fixtures;
using TechCurse.Infrastructure.Data;
using Xunit;

namespace TechCurse.Api.IntegrationTests.Persistence;

/// <summary>
/// Garante que o chaveiro do Data Protection é gravado no banco, e não no diretório
/// efêmero <c>~/.aspnet/DataProtection-Keys</c> do container.
/// </summary>
public class DataProtectionKeyPersistenceTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public DataProtectionKeyPersistenceTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Protect_ShouldGravarChaveNoDbContext_EFazerRoundTrip()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>();
        var context = scope.ServiceProvider.GetRequiredService<TechCurseContext>();

        var protector = provider.CreateProtector("TechCurse.Testes.DataProtection");

        // Act — o primeiro Protect força a criação do chaveiro.
        var protegido = protector.Protect("conteudo-sensivel");

        // Assert
        protector.Unprotect(protegido).Should().Be("conteudo-sensivel");
        (await context.DataProtectionKeys.CountAsync()).Should().BeGreaterThan(0);
    }
}
