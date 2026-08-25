using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
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
        // Arrange
        await _factory.EnsureRolesCreatedAsync();
        var client = _factory.CreateAnonymousClient();
        var email = $"new_auth_user_{Guid.NewGuid():N}@techcurse.com";
        var input = new RegisterInputDto("NovoUsuario", email, UserRole.Student, "SenhaForte@123", "SenhaForte@123");

        // Act
        var response = await client.PostAsJsonAsync("/tech-curse/Auth/register", input);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Login_WhenCredentialsValid_ShouldReturn200OK_WithToken()
    {
        // Arrange
        await _factory.EnsureRolesCreatedAsync();
        var client = _factory.CreateAnonymousClient();
        var email = $"login_user_{Guid.NewGuid():N}@techcurse.com";
        var password = "SenhaForte@123";

        // Create user using UserManager
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

        // Act
        var response = await client.PostAsJsonAsync("/tech-curse/Auth/login", input);

        // Assert
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
        // Arrange
        var client = _factory.CreateAnonymousClient();
        var input = new LoginInputDto("nonexistent@techcurse.com", "WrongPassword@123");

        // Act
        var response = await client.PostAsJsonAsync("/tech-curse/Auth/login", input);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Login_WhenCredentialsValid_ShouldPersistApenasOHashDoRefreshToken()
    {
        // Arrange
        var client = _factory.CreateAnonymousClient();
        var (email, senha) = await CriarUsuarioAsync();

        // Act
        var response = await client.PostAsJsonAsync("/tech-curse/Auth/login", new LoginInputDto(email, senha));
        var authOutput = await response.Content.ReadFromJsonAsync<AuthOutputDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = await userManager.FindByEmailAsync(email);

        var valorPersistido = await userManager.GetAuthenticationTokenAsync(user!, "JWTApp", "RefreshToken");

        // O que está no banco é o SHA-256 (32 bytes), não o token de 64 bytes emitido.
        valorPersistido.Should().NotBeNullOrEmpty();
        valorPersistido.Should().NotBe(authOutput!.RefreshToken);
        Convert.FromBase64String(valorPersistido!).Should().HaveCount(32);

        // E o refresh token ganhou expiração própria, no futuro.
        var expiracaoPersistida = await userManager.GetAuthenticationTokenAsync(user!, "JWTApp", "RefreshTokenExpiry");
        expiracaoPersistida.Should().NotBeNullOrEmpty();
        DateTimeOffset.Parse(expiracaoPersistida!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            .Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Refresh_WhenRefreshTokenValido_ShouldReturn200_ComTokensRotacionados()
    {
        // Arrange
        var client = _factory.CreateAnonymousClient();
        var (email, senha) = await CriarUsuarioAsync();

        var loginResponse = await client.PostAsJsonAsync("/tech-curse/Auth/login", new LoginInputDto(email, senha));
        var login = await loginResponse.Content.ReadFromJsonAsync<AuthOutputDto>();

        // Act
        var response = await client.PostAsJsonAsync(
            "/tech-curse/Auth/refresh",
            new RefreshTokenInputDto(login!.AccessToken, login.RefreshToken));

        // Assert
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
        // Arrange
        var client = _factory.CreateAnonymousClient();
        var (email, senha) = await CriarUsuarioAsync();

        var loginResponse = await client.PostAsJsonAsync("/tech-curse/Auth/login", new LoginInputDto(email, senha));
        var login = await loginResponse.Content.ReadFromJsonAsync<AuthOutputDto>();

        var refreshTokenForjado = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

        // Act
        var response = await client.PostAsJsonAsync(
            "/tech-curse/Auth/refresh",
            new RefreshTokenInputDto(login!.AccessToken, refreshTokenForjado));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Cria um usuário Student com e-mail único e devolve as credenciais.
    /// </summary>
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
}
