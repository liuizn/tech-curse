using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using TechCurse.src.Application.Features.Courses.Commands.CreateCourse;
using TechCurse.Test.Integration.Fixtures;
using Xunit;

namespace TechCurse.Test.Integration.Middlewares;

public class ExceptionHandlingMiddlewareTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ExceptionHandlingMiddlewareTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task WhenValidationFails_ShouldReturn422WithProblemDetailsAndErrors()
    {
        // Arrange
        var client = _factory.CreateAdminClient();
        var invalidCommand = new CreateCourseCommand("", "", "", -1);

        // Act
        var response = await client.PostAsJsonAsync("/tech-curse/Course", invalidCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be((int)HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task WhenResourceNotFound_ShouldReturn404WithProblemDetails()
    {
        // Arrange
        var client = _factory.CreateAdminClient();

        // Act
        var response = await client.GetAsync("/tech-curse/Course/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be((int)HttpStatusCode.NotFound);
    }
}
