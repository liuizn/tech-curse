using System.Net;
using System.Text.Json;
using FluentAssertions;
using TechCurse.Api.IntegrationTests.Fixtures;

namespace TechCurse.Api.IntegrationTests.Endpoints;

public class SwaggerDocumentTests : IClassFixture<SwaggerDocumentTests.HomologFactory>
{
    private readonly HomologFactory _factory;

    public SwaggerDocumentTests(HomologFactory factory)
    {
        _factory = factory;
    }

    public class HomologFactory : CustomWebApplicationFactory
    {
        protected override string EnvironmentName => "Homolog";
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetSwaggerDocument_ShouldReturnValidOpenApiJson()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

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
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var payload = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(payload);
        var schemes = document.RootElement
            .GetProperty("components")
            .GetProperty("securitySchemes");

        schemes.TryGetProperty("Bearer", out _)
            .Should().BeTrue("a autenticação JWT precisa aparecer no documento OpenAPI");
    }
}
