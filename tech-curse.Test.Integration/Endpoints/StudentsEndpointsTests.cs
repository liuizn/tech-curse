using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using TechCurse.src.Application.DTOs;
using TechCurse.src.Domain.Entities;
using TechCurse.Test.Integration.Fixtures;
using Xunit;

namespace TechCurse.Test.Integration.Endpoints;

public class StudentsEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public StudentsEndpointsTests(CustomWebApplicationFactory factory)
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
        var response = await client.GetAsync("/tech-curse/Student");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetAll_WhenUserIsStudent_ShouldReturn403Forbidden()
    {
        // Arrange
        var client = _factory.CreateStudentClient();

        // Act
        var response = await client.GetAsync("/tech-curse/Student");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetAll_WhenUserIsAdmin_ShouldReturn200OK()
    {
        // Arrange
        var client = _factory.CreateAdminClient();

        // Act
        var response = await client.GetAsync("/tech-curse/Student?pageNumber=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetById_WhenStudentNotFound_ShouldReturn404NotFound()
    {
        // Arrange
        var client = _factory.CreateAdminClient();

        // Act
        var response = await client.GetAsync("/tech-curse/Student/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetSelf_WhenStudentAuthenticated_ShouldReturn200OK()
    {
        // Arrange
        var email = "student_me@techcurse.com";
        var userId = "student-me-id";
        var client = _factory.CreateStudentClient(email, userId);

        await _factory.ExecuteDbContextAsync(async context =>
        {
            var user = new IdentityUser { Id = userId, Email = email, UserName = email, NormalizedEmail = email.ToUpperInvariant(), NormalizedUserName = email.ToUpperInvariant() };
            context.Users.Add(user);
            context.Students.Add(new Student
            {
                Nome = "Aluno Me",
                Email = email,
                IdentityUserId = userId,
                IsDeleted = false,
                DataCadastro = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        });

        // Act
        var response = await client.GetAsync("/tech-curse/Student/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var studentDto = await response.Content.ReadFromJsonAsync<StudentOutputDto>();
        studentDto.Should().NotBeNull();
        studentDto!.Email.Should().Be(email);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Post_WhenPayloadInvalid_ShouldReturn422UnprocessableEntity()
    {
        // Arrange
        var client = _factory.CreateAdminClient();
        var input = new StudentPostDto("", "invalid-email");

        // Act
        var response = await client.PostAsJsonAsync("/tech-curse/Student", input);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Post_WhenValidAndUserExists_ShouldReturn201Created()
    {
        // Arrange
        var client = _factory.CreateAdminClient();
        var email = "novo_aluno_post@techcurse.com";
        var userId = "novo-aluno-user-id";

        await _factory.ExecuteDbContextAsync(async context =>
        {
            var user = new IdentityUser
            {
                Id = userId,
                Email = email,
                UserName = email,
                NormalizedEmail = email.ToUpperInvariant(),
                NormalizedUserName = email.ToUpperInvariant()
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();
        });

        var input = new StudentPostDto("Novo Aluno", email);

        // Act
        var response = await client.PostAsJsonAsync("/tech-curse/Student", input);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<StudentOutputDto>();
        created.Should().NotBeNull();
        created!.Nome.Should().Be("Novo Aluno");
        created.Email.Should().Be(email);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Put_WhenStudentUpdatesName_ShouldReturn204NoContent()
    {
        // Arrange
        var email = "student_put@techcurse.com";
        var userId = "student-put-id";
        var client = _factory.CreateStudentClient(email, userId);

        int studentId = 0;
        await _factory.ExecuteDbContextAsync(async context =>
        {
            var user = new IdentityUser { Id = userId, Email = email, UserName = email, NormalizedEmail = email.ToUpperInvariant(), NormalizedUserName = email.ToUpperInvariant() };
            context.Users.Add(user);
            var student = new Student
            {
                Nome = "Nome Antigo",
                Email = email,
                IdentityUserId = userId,
                IsDeleted = false,
                DataCadastro = DateTime.UtcNow
            };
            context.Students.Add(student);
            await context.SaveChangesAsync();
            studentId = student.StudentId;
        });

        var input = new StudentPutDto("Nome Atualizado");

        // Act
        var response = await client.PutAsJsonAsync($"/tech-curse/Student/{studentId}", input);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Delete_WhenAdminDeletesStudent_ShouldReturn204NoContent()
    {
        // Arrange
        var client = _factory.CreateAdminClient();
        var email = "student_del@techcurse.com";
        var userId = "student-del-id";

        int studentId = 0;
        await _factory.ExecuteDbContextAsync(async context =>
        {
            var user = new IdentityUser { Id = userId, Email = email, UserName = email, NormalizedEmail = email.ToUpperInvariant(), NormalizedUserName = email.ToUpperInvariant() };
            context.Users.Add(user);
            var student = new Student
            {
                Nome = "Aluno Del",
                Email = email,
                IdentityUserId = userId,
                IsDeleted = false,
                DataCadastro = DateTime.UtcNow
            };
            context.Students.Add(student);
            await context.SaveChangesAsync();
            studentId = student.StudentId;
        });

        // Act
        var response = await client.DeleteAsync($"/tech-curse/Student/{studentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
