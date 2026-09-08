using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TechCurse.Api.IntegrationTests.Fixtures;
using TechCurse.Application.DTOs;
using Xunit;

namespace TechCurse.Api.IntegrationTests.Endpoints;

public class CorsTests : IClassFixture<CorsEnabledWebApplicationFactory>
{
    private readonly CorsEnabledWebApplicationFactory _factory;

    public CorsTests(CorsEnabledWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Preflight_QuandoOrigemPermitida_DeveLiberarAOrigem()
    {
        var client = _factory.CreateAnonymousClient();
        var request = MontarPreflight("/tech-curse/Course", CorsEnabledWebApplicationFactory.OrigemPermitida);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.GetValues("Access-Control-Allow-Origin")
            .Should().ContainSingle().Which
            .Should().Be(CorsEnabledWebApplicationFactory.OrigemPermitida);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Preflight_QuandoOrigemNaoPermitida_NaoDeveLiberarAOrigem()
    {
        var client = _factory.CreateAnonymousClient();
        var request = MontarPreflight("/tech-curse/Course", CorsEnabledWebApplicationFactory.OrigemNaoPermitida);

        var response = await client.SendAsync(request);

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RequisicaoReal_DeveExporCorrelationIdERetryAfter()
    {
        var client = _factory.CreateAnonymousClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/tech-curse/Course");
        request.Headers.Add("Origin", CorsEnabledWebApplicationFactory.OrigemPermitida);

        var response = await client.SendAsync(request);

        var expostos = response.Headers.GetValues("Access-Control-Expose-Headers").ToList();

        expostos.Should().Contain(valor => valor.Contains("X-Correlation-ID"));
        expostos.Should().Contain(valor => valor.Contains("Retry-After"));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RequisicaoReal_QuandoOrigemPermitida_DeveResponderComOsCabecalhosDeCors()
    {
        var client = _factory.CreateAnonymousClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/tech-curse/Auth/login")
        {
            Content = JsonContent.Create(new LoginInputDto("inexistente@techcurse.com", "SenhaErrada@123"))
        };
        request.Headers.Add("Origin", CorsEnabledWebApplicationFactory.OrigemPermitida);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.GetValues("Access-Control-Allow-Origin")
            .Should().ContainSingle().Which
            .Should().Be(CorsEnabledWebApplicationFactory.OrigemPermitida);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Preflight_QuandoOrigemConfiguradaComBarraFinal_DeveSerNormalizada()
    {
        var client = _factory.CreateAnonymousClient();
        var request = MontarPreflight("/tech-curse/Course", "http://localhost:8081");

        var response = await client.SendAsync(request);

        response.Headers.GetValues("Access-Control-Allow-Origin")
            .Should().ContainSingle().Which
            .Should().Be("http://localhost:8081");
    }

    private static HttpRequestMessage MontarPreflight(string rota, string origem)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, rota);
        request.Headers.Add("Origin", origem);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "authorization");
        return request;
    }
}
