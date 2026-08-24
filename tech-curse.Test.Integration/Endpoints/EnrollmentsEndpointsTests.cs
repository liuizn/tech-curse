using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using TechCurse.src.Application.DTOs;
using TechCurse.src.Domain.Entities;
using TechCurse.Test.Integration.Fixtures;
using Xunit;

namespace TechCurse.Test.Integration.Endpoints;

public class EnrollmentsEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public EnrollmentsEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Post_WhenUnauthenticated_ShouldReturn401Unauthorized()
    {
        // Arrange
        var client = _factory.CreateAnonymousClient();
        var input = new EnrollmentInputDto(1, 1);

        // Act
        var response = await client.PostAsJsonAsync("/tech-curse/Enrollment", input);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Post_WhenPayloadIsInvalid_ShouldReturn422UnprocessableEntity()
    {
        // Arrange: Admin client sending 0 for IDs triggers FluentValidation
        var client = _factory.CreateAdminClient();
        var invalidInput = new EnrollmentInputDto(0, 0);

        // Act
        var response = await client.PostAsJsonAsync("/tech-curse/Enrollment", invalidInput);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Post_WhenCourseNotFound_ShouldReturn404NotFound()
    {
        // Arrange
        var studentEmail = $"student_nf_{Guid.NewGuid():N}@techcurse.com";
        var studentId = $"student-guid-{Guid.NewGuid():N}";
        var client = _factory.CreateStudentClient(studentEmail, studentId);

        int dbStudentId = 0;
        await _factory.ExecuteDbContextAsync(async context =>
        {
            var user = new IdentityUser { Id = studentId, Email = studentEmail, UserName = studentEmail, NormalizedEmail = studentEmail.ToUpperInvariant(), NormalizedUserName = studentEmail.ToUpperInvariant() };
            context.Users.Add(user);
            var student = new Student
            {
                Nome = "Aluno Teste",
                Email = studentEmail,
                IdentityUserId = studentId,
                IsDeleted = false,
                DataCadastro = DateTime.UtcNow
            };
            context.Students.Add(student);
            await context.SaveChangesAsync();
            dbStudentId = student.StudentId;
        });

        // CourseId = 99999 (not found)
        var input = new EnrollmentInputDto(99999, dbStudentId);

        // Act
        var response = await client.PostAsJsonAsync("/tech-curse/Enrollment", input);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Post_WhenValid_ShouldReturn202Accepted()
    {
        // Arrange
        var studentEmail = $"student_val_{Guid.NewGuid():N}@techcurse.com";
        var studentId = $"student-guid-{Guid.NewGuid():N}";
        var client = _factory.CreateStudentClient(studentEmail, studentId);

        int courseId = 0;
        int dbStudentId = 0;
        await _factory.ExecuteDbContextAsync(async context =>
        {
            var user = new IdentityUser { Id = studentId, Email = studentEmail, UserName = studentEmail, NormalizedEmail = studentEmail.ToUpperInvariant(), NormalizedUserName = studentEmail.ToUpperInvariant() };
            context.Users.Add(user);
            var student = new Student
            {
                Nome = "Aluno Válido",
                Email = studentEmail,
                IdentityUserId = studentId,
                IsDeleted = false,
                DataCadastro = DateTime.UtcNow
            };
            context.Students.Add(student);

            var course = new Course
            {
                Titulo = $"Curso Matrícula Válida {Guid.NewGuid():N}",
                Descricao = "Desc",
                Categoria = "Tech",
                CargaHoraria = 20,
                DataCriacao = DateTime.UtcNow
            };
            context.Courses.Add(course);

            await context.SaveChangesAsync();
            courseId = course.CourseId;
            dbStudentId = student.StudentId;
        });

        var input = new EnrollmentInputDto(courseId, dbStudentId);

        // Act
        var response = await client.PostAsJsonAsync("/tech-curse/Enrollment", input);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Post_WhenAlreadyEnrolled_ShouldReturn409Conflict()
    {
        // Arrange
        var studentEmail = $"student_cnf_{Guid.NewGuid():N}@techcurse.com";
        var studentId = $"student-guid-{Guid.NewGuid():N}";
        var client = _factory.CreateStudentClient(studentEmail, studentId);

        int courseId = 0;
        int dbStudentId = 0;
        await _factory.ExecuteDbContextAsync(async context =>
        {
            var user = new IdentityUser { Id = studentId, Email = studentEmail, UserName = studentEmail, NormalizedEmail = studentEmail.ToUpperInvariant(), NormalizedUserName = studentEmail.ToUpperInvariant() };
            context.Users.Add(user);
            var student = new Student
            {
                Nome = "Aluno Conflito",
                Email = studentEmail,
                IdentityUserId = studentId,
                IsDeleted = false,
                DataCadastro = DateTime.UtcNow
            };
            context.Students.Add(student);

            var course = new Course
            {
                Titulo = $"Curso Matrícula Conflito {Guid.NewGuid():N}",
                Descricao = "Desc",
                Categoria = "Tech",
                CargaHoraria = 20,
                DataCriacao = DateTime.UtcNow
            };
            context.Courses.Add(course);
            await context.SaveChangesAsync();

            context.Enrollments.Add(new Enrollment
            {
                StudentId = student.StudentId,
                CourseId = course.CourseId,
                DataMatricula = DateTime.UtcNow,
                Status = false
            });
            await context.SaveChangesAsync();

            courseId = course.CourseId;
            dbStudentId = student.StudentId;
        });

        var input = new EnrollmentInputDto(courseId, dbStudentId);

        // Act
        var response = await client.PostAsJsonAsync("/tech-curse/Enrollment", input);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
