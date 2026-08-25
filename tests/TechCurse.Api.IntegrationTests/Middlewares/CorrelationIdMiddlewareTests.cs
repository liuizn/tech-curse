using System.Net;
using FluentAssertions;
using TechCurse.Api.IntegrationTests.Fixtures;
using Xunit;

namespace TechCurse.Api.IntegrationTests.Middlewares;

public class CorrelationIdMiddlewareTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public CorrelationIdMiddlewareTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Request_WhenNoCorrelationIdHeaderSent_ShouldGenerateAndReturnCorrelationId()
    {
        // Arrange
        var client = _factory.CreateAnonymousClient();

        // Act
        var response = await client.GetAsync("/health/live");

        // Assert
        response.Headers.Should().ContainKey("X-Correlation-ID");
        var correlationId = response.Headers.GetValues("X-Correlation-ID").FirstOrDefault();
        correlationId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Request_WhenCorrelationIdHeaderSent_ShouldPreserveAndReturnSameCorrelationId()
    {
        // Arrange
        var client = _factory.CreateAnonymousClient();
        var customCorrelationId = "custom-correlation-id-987654";
        client.DefaultRequestHeaders.Add("X-Correlation-ID", customCorrelationId);

        // Act
        var response = await client.GetAsync("/health/live");

        // Assert
        response.Headers.Should().ContainKey("X-Correlation-ID");
        var returnedId = response.Headers.GetValues("X-Correlation-ID").FirstOrDefault();
        returnedId.Should().Be(customCorrelationId);
    }
}
