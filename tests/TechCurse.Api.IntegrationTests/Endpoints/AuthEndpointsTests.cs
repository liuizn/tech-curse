using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using TechCurse.Api.IntegrationTests.Fixtures;
using TechCurse.Application.DTOs;
using TechCurse.Domain.Enums;
using Xunit;

namespace TechCurse.Api.IntegrationTests.Endpoints;

public class AuthEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Register_WhenValid_ShouldReturn201Created()
    {
        await _factory.EnsureRolesCreatedAsync();
        var client = _factory.CreateAnonymousClient();
        var email = $"new_auth_user_{Guid.NewGuid():N}@techcurse.com";
        var input = new RegisterInputDto("NovoUsuario", email, "SenhaForte@123", "SenhaForte@123");

        var response = await client.PostAsJsonAsync("/tech-curse/Auth/register", input);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Login_WhenCredentialsValid_ShouldReturn200OK_WithToken()
    {
        await _factory.EnsureRolesCreatedAsync();
        var client = _factory.CreateAnonymousClient();
        var email = $"login_user_{Guid.NewGuid():N}@techcurse.com";
        var password = "SenhaForte@123";

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var user = new IdentityUser { UserName = email, Email = email };
            var result = await userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, "Student");
            }
        }

        var input = new LoginInputDto(email, password);

        var response = await client.PostAsJsonAsync("/tech-curse/Auth/login", input);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var authOutput = await response.Content.ReadFromJsonAsync<AuthOutputDto>();
        authOutput.Should().NotBeNull();
        authOutput!.AccessToken.Should().NotBeNullOrEmpty();
        authOutput.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Login_WhenPasswordInvalid_ShouldReturn401Unauthorized()
    {
        var client = _factory.CreateAnonymousClient();
        var input = new LoginInputDto("nonexistent@techcurse.com", "WrongPassword@123");

        var response = await client.PostAsJsonAsync("/tech-curse/Auth/login", input);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Login_WhenCredentialsValid_ShouldPersistApenasOHashDoRefreshToken()
    {
        var client = _factory.CreateAnonymousClient();
        var (email, senha) = await CriarUsuarioAsync();

        var response = await client.PostAsJsonAsync("/tech-curse/Auth/login", new LoginInputDto(email, senha));
        var authOutput = await response.Content.ReadFromJsonAsync<AuthOutputDto>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = await userManager.FindByEmailAsync(email);

        var valorPersistido = await userManager.GetAuthenticationTokenAsync(user!, "JWTApp", "RefreshToken");

        valorPersistido.Should().NotBeNullOrEmpty();
        valorPersistido.Should().NotBe(authOutput!.RefreshToken);
        Convert.FromBase64String(valorPersistido!).Should().HaveCount(32);

        var expiracaoPersistida = await userManager.GetAuthenticationTokenAsync(user!, "JWTApp", "RefreshTokenExpiry");
        expiracaoPersistida.Should().NotBeNullOrEmpty();
        DateTimeOffset.Parse(expiracaoPersistida!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            .Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Refresh_WhenRefreshTokenValido_ShouldReturn200_ComTokensRotacionados()
    {
        var client = _factory.CreateAnonymousClient();
        var (email, senha) = await CriarUsuarioAsync();

        var loginResponse = await client.PostAsJsonAsync("/tech-curse/Auth/login", new LoginInputDto(email, senha));
        var login = await loginResponse.Content.ReadFromJsonAsync<AuthOutputDto>();

        var response = await client.PostAsJsonAsync(
            "/tech-curse/Auth/refresh",
            new RefreshTokenInputDto(login!.AccessToken, login.RefreshToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var refresh = await response.Content.ReadFromJsonAsync<AuthOutputDto>();
        refresh.Should().NotBeNull();
        refresh!.RefreshToken.Should().NotBeNullOrEmpty();
        refresh.RefreshToken.Should().NotBe(login.RefreshToken, "o refresh token é rotacionado a cada uso");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Refresh_WhenRefreshTokenNaoConfere_ShouldReturn401Unauthorized()
    {
        var client = _factory.CreateAnonymousClient();
        var (email, senha) = await CriarUsuarioAsync();

        var loginResponse = await client.PostAsJsonAsync("/tech-curse/Auth/login", new LoginInputDto(email, senha));
        var login = await loginResponse.Content.ReadFromJsonAsync<AuthOutputDto>();

        var refreshTokenForjado = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

        var response = await client.PostAsJsonAsync(
            "/tech-curse/Auth/refresh",
            new RefreshTokenInputDto(login!.AccessToken, refreshTokenForjado));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<(string Email, string Senha)> CriarUsuarioAsync()
    {
        await _factory.EnsureRolesCreatedAsync();

        var email = $"refresh_user_{Guid.NewGuid():N}@techcurse.com";
        const string senha = "SenhaForte@123";

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        var user = new IdentityUser { UserName = email, Email = email };
        var result = await userManager.CreateAsync(user, senha);

        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(user, "Student");
        }

        return (email, senha);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Register_WhenCorpoTrazRoleAdmin_ShouldCriarUsuarioApenasComoStudent()
    {
        await _factory.EnsureRolesCreatedAsync();
        var client = _factory.CreateAnonymousClient();
        var email = $"tentativa_admin_{Guid.NewGuid():N}@techcurse.com";
        var corpo = new
        {
            name = $"tentativa{Guid.NewGuid():N}",
            email,
            role = "Admin",
            password = "SenhaForte@123",
            confirmPassword = "SenhaForte@123"
        };

        var response = await client.PostAsJsonAsync("/tech-curse/Auth/register", corpo);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var usuario = await userManager.FindByEmailAsync(email);
        usuario.Should().NotBeNull();
        var roles = await userManager.GetRolesAsync(usuario!);
        roles.Should().BeEquivalentTo(new[] { "Student" });
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateUser_WhenAnonimo_ShouldReturn401Unauthorized()
    {
        await _factory.EnsureRolesCreatedAsync();
        var client = _factory.CreateAnonymousClient();
        var input = new CreateUserInputDto($"anonimo{Guid.NewGuid():N}", $"anonimo_{Guid.NewGuid():N}@techcurse.com", UserRole.Instructor, "SenhaForte@123", "SenhaForte@123");

        var response = await client.PostAsJsonAsync("/tech-curse/Auth/users", input);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateUser_WhenStudent_ShouldReturn403Forbidden()
    {
        await _factory.EnsureRolesCreatedAsync();
        var client = _factory.CreateStudentClient();
        var input = new CreateUserInputDto($"aluno{Guid.NewGuid():N}", $"aluno_{Guid.NewGuid():N}@techcurse.com", UserRole.Admin, "SenhaForte@123", "SenhaForte@123");

        var response = await client.PostAsJsonAsync("/tech-curse/Auth/users", input);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateUser_WhenAdmin_ShouldReturn201_ECriarNaRolePedida()
    {
        await _factory.EnsureRolesCreatedAsync();
        var client = _factory.CreateAdminClient();
        var email = $"instrutor_{Guid.NewGuid():N}@techcurse.com";
        var input = new CreateUserInputDto($"instrutor{Guid.NewGuid():N}", email, UserRole.Instructor, "SenhaForte@123", "SenhaForte@123");

        var response = await client.PostAsJsonAsync("/tech-curse/Auth/users", input);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var usuario = await userManager.FindByEmailAsync(email);
        usuario.Should().NotBeNull();
        var roles = await userManager.GetRolesAsync(usuario!);
        roles.Should().BeEquivalentTo(new[] { "Instructor" });
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateUser_WhenEmailEmUso_ShouldReturn422_ComDuplicateEmail()
    {
        await _factory.EnsureRolesCreatedAsync();
        var client = _factory.CreateAdminClient();
        var email = $"repetido_{Guid.NewGuid():N}@techcurse.com";
        var primeiro = new CreateUserInputDto($"primeiro{Guid.NewGuid():N}", email, UserRole.Instructor, "SenhaForte@123", "SenhaForte@123");
        var segundo = new CreateUserInputDto($"segundo{Guid.NewGuid():N}", email, UserRole.Instructor, "SenhaForte@123", "SenhaForte@123");
        (await client.PostAsJsonAsync("/tech-curse/Auth/users", primeiro)).StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await client.PostAsJsonAsync("/tech-curse/Auth/users", segundo);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using var documento = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        documento.RootElement.GetProperty("errors").TryGetProperty("DuplicateEmail", out _).Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateUser_WhenRoleInexistente_ShouldReturn422_ENaoCriarUsuario()
    {
        await _factory.EnsureRolesCreatedAsync();
        var client = _factory.CreateAdminClient();
        var email = $"role_invalida_{Guid.NewGuid():N}@techcurse.com";
        var corpo = new
        {
            name = $"roleinvalida{Guid.NewGuid():N}",
            email,
            role = 99,
            password = "SenhaForte@123",
            confirmPassword = "SenhaForte@123"
        };

        var response = await client.PostAsJsonAsync("/tech-curse/Auth/users", corpo);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using var documento = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        documento.RootElement.GetProperty("errors").TryGetProperty("Role", out _).Should().BeTrue();
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var usuario = await userManager.FindByEmailAsync(email);
        usuario.Should().BeNull();
    }
}
