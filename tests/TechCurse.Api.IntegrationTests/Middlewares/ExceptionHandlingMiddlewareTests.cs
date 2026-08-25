using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using TechCurse.Api.IntegrationTests.Fixtures;
using TechCurse.Application.Features.Courses.Commands.CreateCourse;
using Xunit;

namespace TechCurse.Api.IntegrationTests.Middlewares;

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
        var client = _factory.CreateAdminClient();
        var invalidCommand = new CreateCourseCommand("", "", "", -1);

        var response = await client.PostAsJsonAsync("/tech-curse/Course", invalidCommand);

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
        var client = _factory.CreateAdminClient();

        var response = await client.GetAsync("/tech-curse/Course/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be((int)HttpStatusCode.NotFound);
    }
}
