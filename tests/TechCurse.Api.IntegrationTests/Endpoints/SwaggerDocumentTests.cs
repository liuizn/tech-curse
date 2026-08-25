using System.Net;
using System.Text.Json;
using FluentAssertions;
using TechCurse.Api.IntegrationTests.Fixtures;

namespace TechCurse.Api.IntegrationTests.Endpoints;

/// <summary>
/// Garante que o documento OpenAPI continua sendo gerado. A suíte não passava
/// pelo Swagger, então quebras na configuração do Swashbuckle (que muda de API
/// entre majors) só apareciam em runtime, ao abrir a UI.
/// </summary>
public class SwaggerDocumentTests : IClassFixture<SwaggerDocumentTests.HomologFactory>
{
    private readonly HomologFactory _factory;

    public SwaggerDocumentTests(HomologFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// O Swagger só é mapeado em Development ou Homolog; o ambiente padrão dos
    /// testes ("Testing") não monta o endpoint.
    /// </summary>
    public class HomologFactory : CustomWebApplicationFactory
    {
        protected override string EnvironmentName => "Homolog";
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetSwaggerDocument_ShouldReturnValidOpenApiJson()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/swagger/v1/swagger.json");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);

        document.RootElement.GetProperty("info").GetProperty("title").GetString()
            .Should().Be("Tech Curse API");
        document.RootElement.GetProperty("paths").EnumerateObject()
            .Should().NotBeEmpty("o documento precisa listar os endpoints da API");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetSwaggerDocument_ShouldDeclareBearerSecurityScheme()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var payload = await response.Content.ReadAsStringAsync();

        // Assert
        using var document = JsonDocument.Parse(payload);
        var schemes = document.RootElement
            .GetProperty("components")
            .GetProperty("securitySchemes");

        schemes.TryGetProperty("Bearer", out _)
            .Should().BeTrue("a autenticação JWT precisa aparecer no documento OpenAPI");
    }
}
