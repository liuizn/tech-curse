using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using TechCurse.Infrastructure.Data;
using Xunit;

namespace TechCurse.Api.IntegrationTests.Persistence;

public class DbInitializerTests
{
    private const string EmailAdmin = "admin.semeado@techcurse.dev";
    private const string SenhaAdmin = "SenhaForte@123";

    private sealed class AmbienteDeTeste : IHostEnvironment
    {
        public AmbienteDeTeste(string nome)
        {
            EnvironmentName = nome;
        }

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "TechCurse.Testes";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static ServiceProvider CriarProvedor(string ambiente, Dictionary<string, string?> configuracao)
    {
        var nomeDoBanco = $"Seed_{Guid.NewGuid():N}";
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<TechCurseContext>(options => options.UseInMemoryDatabase(nomeDoBanco));
        services.AddIdentityCore<IdentityUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<TechCurseContext>();
        services.AddSingleton<IHostEnvironment>(new AmbienteDeTeste(ambiente));
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(configuracao).Build());
        return services.BuildServiceProvider();
    }

    private static Dictionary<string, string?> ConfiguracaoCompleta(string senha = SenhaAdmin) => new()
    {
        ["Seed:Admin:Email"] = EmailAdmin,
        ["Seed:Admin:Password"] = senha
    };

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SeedData_WhenDevelopmentComConfiguracao_ShouldCriarAdmin()
    {
        await using var provedor = CriarProvedor(Environments.Development, ConfiguracaoCompleta());
        using var scope = provedor.CreateScope();

        await DbInitializer.SeedDataAsync(scope.ServiceProvider);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var admin = await userManager.FindByEmailAsync(EmailAdmin);
        admin.Should().NotBeNull();
        admin!.UserName.Should().Be("admin.semeado");
        (await userManager.GetRolesAsync(admin)).Should().BeEquivalentTo(new[] { "Admin" });
        (await userManager.CheckPasswordAsync(admin, SenhaAdmin)).Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SeedData_WhenForaDeDevelopment_ShouldNaoCriarAdmin()
    {
        await using var provedor = CriarProvedor(Environments.Production, ConfiguracaoCompleta());
        using var scope = provedor.CreateScope();

        await DbInitializer.SeedDataAsync(scope.ServiceProvider);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        (await userManager.FindByEmailAsync(EmailAdmin)).Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SeedData_WhenSemConfiguracao_ShouldNaoCriarAdmin_ENaoFalhar()
    {
        await using var provedor = CriarProvedor(Environments.Development, new Dictionary<string, string?>());
        using var scope = provedor.CreateScope();

        var acao = () => DbInitializer.SeedDataAsync(scope.ServiceProvider);

        await acao.Should().NotThrowAsync();
        var context = scope.ServiceProvider.GetRequiredService<TechCurseContext>();
        (await context.Users.CountAsync()).Should().Be(0);
        (await context.Roles.CountAsync()).Should().Be(3);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SeedData_WhenExecutadoDuasVezes_ShouldNaoDuplicarNemAlterarAdmin()
    {
        await using var provedor = CriarProvedor(Environments.Development, ConfiguracaoCompleta());
        using (var primeiroEscopo = provedor.CreateScope())
        {
            await DbInitializer.SeedDataAsync(primeiroEscopo.ServiceProvider);
        }

        using var scope = provedor.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TechCurseContext>();
        var hashAntes = (await context.Users.SingleAsync(u => u.Email == EmailAdmin)).PasswordHash;

        await DbInitializer.SeedDataAsync(scope.ServiceProvider);

        (await context.Users.CountAsync(u => u.Email == EmailAdmin)).Should().Be(1);
        (await context.Users.AsNoTracking().SingleAsync(u => u.Email == EmailAdmin)).PasswordHash.Should().Be(hashAntes);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SeedData_WhenSenhaForaDaPolitica_ShouldLancarComOsErrosDoIdentity()
    {
        await using var provedor = CriarProvedor(Environments.Development, ConfiguracaoCompleta("fraca"));
        using var scope = provedor.CreateScope();

        var acao = () => DbInitializer.SeedDataAsync(scope.ServiceProvider);

        await acao.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Admin semeado*");
    }
}
