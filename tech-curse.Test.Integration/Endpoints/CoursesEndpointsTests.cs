using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TechCurse.src.Application.DTOs;
using TechCurse.src.Application.Features.Courses.Commands.CreateCourse;
using TechCurse.src.Domain.Entities;
using TechCurse.Test.Integration.Fixtures;
using Xunit;

namespace TechCurse.Test.Integration.Endpoints;

public class CoursesEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public CoursesEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetAll_WhenUnauthenticated_ShouldReturn401Unauthorized()
    {
        // Arrange
        var client = _factory.CreateAnonymousClient();

        // Act
        var response = await client.GetAsync("/tech-curse/Course");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetAll_WhenAuthenticated_ShouldReturn200OK_WithCoursesList()
    {
        // Arrange
        var client = _factory.CreateAdminClient();

        await _factory.ExecuteDbContextAsync(async context =>
        {
            context.Courses.Add(new Course
            {
                Titulo = "Curso .NET 10",
                Descricao = "Aprenda .NET 10 Avançado",
                Categoria = "Backend",
                CargaHoraria = 50,
                DataCriacao = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        });

        // Act
        var response = await client.GetAsync("/tech-curse/Course?pageNumber=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<PagedResultDto<CourseOutputDto>>();
        content.Should().NotBeNull();
        content!.Items.Should().NotBeEmpty();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetById_WhenCourseNotFound_ShouldReturn404NotFound()
    {
        // Arrange
        var client = _factory.CreateAdminClient();

        // Act
        var response = await client.GetAsync("/tech-curse/Course/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Post_WhenUserIsStudent_ShouldReturn403Forbidden()
    {
        // Arrange
        var client = _factory.CreateStudentClient();
        var command = new CreateCourseCommand("Curso Hacker", "Desc", "Segurança", 20);

        // Act
        var response = await client.PostAsJsonAsync("/tech-curse/Course", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Post_WhenPayloadIsInvalid_ShouldReturn422UnprocessableEntity()
    {
        // Arrange
        var client = _factory.CreateAdminClient();
        var invalidCommand = new CreateCourseCommand("", "", "", -5);

        // Act
        var response = await client.PostAsJsonAsync("/tech-curse/Course", invalidCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Post_WhenAdminCreatesCourse_ShouldReturn201Created()
    {
        // Arrange
        var client = _factory.CreateAdminClient();
        var command = new CreateCourseCommand("Curso Arquitetura Limpa", "Domine Clean Architecture", "Arquitetura", 60);

        // Act
        var response = await client.PostAsJsonAsync("/tech-curse/Course", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<CourseOutputDto>();
        created.Should().NotBeNull();
        created!.Titulo.Should().Be("Curso Arquitetura Limpa");
        response.Headers.Location.Should().NotBeNull();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Put_WhenAdminUpdatesCourse_ShouldReturn204NoContent()
    {
        // Arrange
        var client = _factory.CreateAdminClient();

        int courseId = 0;
        await _factory.ExecuteDbContextAsync(async context =>
        {
            var course = new Course
            {
                Titulo = "Curso Original",
                Descricao = "Original",
                Categoria = "Dev",
                CargaHoraria = 10,
                DataCriacao = DateTime.UtcNow
            };
            context.Courses.Add(course);
            await context.SaveChangesAsync();
            courseId = course.CourseId;
        });

        var updateDto = new CoursePostDto("Curso Alterado", "Descrição Alterada", "Dev", 25);

        // Act
        var response = await client.PutAsJsonAsync($"/tech-curse/Course/{courseId}", updateDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Delete_WhenAdminDeletesCourseWithoutEnrollments_ShouldReturn204NoContent()
    {
        // Arrange
        var client = _factory.CreateAdminClient();

        int courseId = 0;
        await _factory.ExecuteDbContextAsync(async context =>
        {
            var course = new Course
            {
                Titulo = "Curso Para Deletar",
                Descricao = "Desc",
                Categoria = "Dev",
                CargaHoraria = 10,
                DataCriacao = DateTime.UtcNow
            };
            context.Courses.Add(course);
            await context.SaveChangesAsync();
            courseId = course.CourseId;
        });

        // Act
        var response = await client.DeleteAsync($"/tech-curse/Course/{courseId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
