using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using TechCurse.src.Application.DTOs;
using TechCurse.src.Domain.Enums;
using TechCurse.Test.Integration.Fixtures;
using Xunit;

namespace TechCurse.Test.Integration.Endpoints;

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
}
