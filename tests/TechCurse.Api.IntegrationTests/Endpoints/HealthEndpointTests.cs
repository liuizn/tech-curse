using System.Net;
using System.Text.Json;
using FluentAssertions;
using TechCurse.Api.IntegrationTests.Fixtures;

namespace TechCurse.Api.IntegrationTests.Endpoints;

/// <summary>
/// Garante a separação entre liveness e readiness.
/// <para>
/// <c>/health/live</c> não pode depender de infraestrutura externa nem vazar
/// detalhe dela: é o sinal que o orquestrador usa para decidir reiniciar o
/// processo. <c>/health/ready</c> reporta o resultado de cada verificação, e não
/// apenas um "Healthy"/"Unhealthy" agregado — o pipeline enxerga só a resposta
/// HTTP, e sem esse detalhe um 503 no CI não diz qual dependência caiu.
/// </para>
/// </summary>
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
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health/live");

        // Assert
        // Liveness não consulta banco nem cache, então o 200 independe de haver
        // infraestrutura acessível no ambiente de teste.
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadAsStringAsync();
        payload.Should().Be("Healthy");
        payload.Should().NotContain("Cache_Redis", "liveness não deve expor as dependências");
        payload.Should().NotContain("Database_SQLServer", "liveness não deve expor as dependências");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetHealthReady_ShouldReturnJsonWithPerCheckDetail()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        // O status agregado depende de haver Redis/SQL acessíveis no ambiente,
        // então o que se afirma aqui é o formato da resposta, não a saúde em si.
        var response = await client.GetAsync("/health/ready");

        // Assert
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var payload = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        root.GetProperty("status").GetString().Should().NotBeNullOrWhiteSpace();
        root.GetProperty("duracaoMs").GetDouble().Should().BeGreaterThanOrEqualTo(0);

        var checks = root.GetProperty("checks").EnumerateArray().ToList();
        checks.Should().NotBeEmpty("cada dependência registrada precisa aparecer individualmente");

        foreach (var check in checks)
        {
            check.GetProperty("nome").GetString().Should().NotBeNullOrWhiteSpace();
            check.GetProperty("status").GetString().Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetHealthReady_ShouldNameTheRegisteredDependencies()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health/ready");
        var payload = await response.Content.ReadAsStringAsync();

        // Assert
        using var document = JsonDocument.Parse(payload);
        var nomes = document.RootElement.GetProperty("checks")
            .EnumerateArray()
            .Select(check => check.GetProperty("nome").GetString())
            .ToList();

        nomes.Should().Contain("Cache_Redis");
    }
}
