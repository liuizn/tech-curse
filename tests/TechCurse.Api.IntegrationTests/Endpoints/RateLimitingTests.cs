using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TechCurse.Api.IntegrationTests.Fixtures;
using TechCurse.Application.DTOs;
using Xunit;

namespace TechCurse.Api.IntegrationTests.Endpoints;

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
        var client = _factory.CreateAnonymousClient();
        var input = new LoginInputDto("forca_bruta@techcurse.com", "SenhaErrada@123");

        for (var tentativa = 0; tentativa < RateLimitedWebApplicationFactory.LimiteDeAutenticacao; tentativa++)
        {
            var permitida = await client.PostAsJsonAsync("/tech-curse/Auth/login", input);
            permitida.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        var bloqueada = await client.PostAsJsonAsync("/tech-curse/Auth/login", input);

        bloqueada.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        bloqueada.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problema = await bloqueada.Content.ReadFromJsonAsync<ProblemDetails>();
        problema.Should().NotBeNull();
        problema!.Status.Should().Be(StatusCodes.Status429TooManyRequests);
        problema.Detail.Should().Contain("Muitas requisições");
    }
}
