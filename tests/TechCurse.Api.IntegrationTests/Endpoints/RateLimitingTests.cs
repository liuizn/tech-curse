using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TechCurse.Api.IntegrationTests.Fixtures;
using TechCurse.Application.DTOs;
using Xunit;

namespace TechCurse.Api.IntegrationTests.Endpoints;

/// <summary>
/// Cobre o rate limiting HTTP dos endpoints de autenticação — a proteção contra
/// força bruta que o lockout do Identity (por usuário) não oferece.
/// </summary>
public class RateLimitingTests : IClassFixture<RateLimitedWebApplicationFactory>
{
    private readonly RateLimitedWebApplicationFactory _factory;

    public RateLimitingTests(RateLimitedWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Login_WhenExcedeLimiteDaJanela_ShouldReturn429TooManyRequests()
    {
        // Arrange
        var client = _factory.CreateAnonymousClient();
        var input = new LoginInputDto("forca_bruta@techcurse.com", "SenhaErrada@123");

        // Act — as primeiras tentativas são recusadas pelas credenciais (401);
        // a que estoura a cota nem chega ao Identity.
        for (var tentativa = 0; tentativa < RateLimitedWebApplicationFactory.LimiteDeAutenticacao; tentativa++)
        {
            var permitida = await client.PostAsJsonAsync("/tech-curse/Auth/login", input);
            permitida.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        var bloqueada = await client.PostAsJsonAsync("/tech-curse/Auth/login", input);

        // Assert
        bloqueada.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        bloqueada.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problema = await bloqueada.Content.ReadFromJsonAsync<ProblemDetails>();
        problema.Should().NotBeNull();
        problema!.Status.Should().Be(StatusCodes.Status429TooManyRequests);
        problema.Detail.Should().Contain("Muitas requisições");
    }
}
