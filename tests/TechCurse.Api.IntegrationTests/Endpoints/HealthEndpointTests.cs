using System.Text.Json;
using FluentAssertions;
using TechCurse.Api.IntegrationTests.Fixtures;

namespace TechCurse.Api.IntegrationTests.Endpoints;

/// <summary>
/// Garante que <c>/health</c> reporta o resultado de cada verificação, e não
/// apenas um "Healthy"/"Unhealthy" agregado. O pipeline enxerga só a resposta
/// HTTP: sem esse detalhe, um 503 no CI não diz qual dependência caiu.
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
    public async Task GetHealth_ShouldReturnJsonWithPerCheckDetail()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        // O status agregado depende de haver Redis/SQL acessíveis no ambiente,
        // então o que se afirma aqui é o formato da resposta, não a saúde em si.
        var response = await client.GetAsync("/health");

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
    public async Task GetHealth_ShouldNameTheRegisteredDependencies()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");
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
