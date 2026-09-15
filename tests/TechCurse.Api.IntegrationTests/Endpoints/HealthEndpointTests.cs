using System.Net;
using System.Text.Json;
using FluentAssertions;
using TechCurse.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TechCurse.Api.IntegrationTests.Endpoints;

public class HealthEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public HealthEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetHealthLive_ShouldReturnOkWithoutDependencyDetail()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadAsStringAsync();
        payload.Should().Be("Healthy");
        payload.Should().NotContain("Cache_Redis");
        payload.Should().NotContain("Database_Postgres");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetHealthReady_QuandoAnonimo_DeveExporApenasOStatusAgregado()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var payload = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        root.GetProperty("status").GetString().Should().NotBeNullOrWhiteSpace();

        root.TryGetProperty("checks", out _).Should().BeFalse();
        root.TryGetProperty("duracaoMs", out _).Should().BeFalse();
        payload.Should().NotContain("Cache_Redis");
        payload.Should().NotContain("Database_Postgres");
        payload.Should().NotContain("erro");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetHealthReady_QuandoAdmin_DeveDetalharCadaVerificacao()
    {
        var client = _factory.CreateAdminClient();

        var response = await client.GetAsync("/health/ready");

        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var payload = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        root.GetProperty("status").GetString().Should().NotBeNullOrWhiteSpace();
        root.GetProperty("duracaoMs").GetDouble().Should().BeGreaterThanOrEqualTo(0);

        var checks = root.GetProperty("checks").EnumerateArray().ToList();
        checks.Should().NotBeEmpty();

        foreach (var check in checks)
        {
            check.GetProperty("nome").GetString().Should().NotBeNullOrWhiteSpace();
            check.GetProperty("status").GetString().Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetHealthReady_QuandoAdmin_DeveNomearAsDependenciasRegistradas()
    {
        var client = _factory.CreateAdminClient();

        var response = await client.GetAsync("/health/ready");
        var payload = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(payload);
        var nomes = document.RootElement.GetProperty("checks")
            .EnumerateArray()
            .Select(check => check.GetProperty("nome").GetString())
            .ToList();

        nomes.Should().Contain("Cache_Redis");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetHealthReady_QuandoStudent_NaoDeveDetalhar()
    {
        var client = _factory.CreateStudentClient();

        var response = await client.GetAsync("/health/ready");
        var payload = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(payload);
        document.RootElement.TryGetProperty("checks", out _).Should().BeFalse();
    }
}
