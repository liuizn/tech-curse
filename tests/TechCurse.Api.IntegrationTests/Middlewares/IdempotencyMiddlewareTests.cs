using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using TechCurse.Api.IntegrationTests.Fixtures;
using TechCurse.Application.DTOs;
using TechCurse.Domain.Entities;
using Xunit;

namespace TechCurse.Api.IntegrationTests.Middlewares;

public class IdempotencyMiddlewareTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public IdempotencyMiddlewareTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task WhenSameIdempotencyKeySentTwice_ShouldReturnCachedResultWithoutRecreation()
    {
        var client = _factory.CreateAdminClient();
        var idempotencyKey = $"idemp-test-{Guid.NewGuid():N}";
        client.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);

        var userId = $"idemp-user-{Guid.NewGuid():N}";
        var email = $"idemp_{Guid.NewGuid():N}@techcurse.com";

        int enrollmentId = 0;
        await _factory.ExecuteDbContextAsync(async context =>
        {
            var user = new IdentityUser { Id = userId, Email = email, UserName = email, NormalizedEmail = email.ToUpperInvariant(), NormalizedUserName = email.ToUpperInvariant() };
            context.Users.Add(user);

            var student = new Student
            {
                Nome = "Aluno Idemp",
                Email = email,
                IdentityUserId = userId,
                IsDeleted = false,
                DataCadastro = DateTime.UtcNow
            };
            context.Students.Add(student);

            var course = new Course
            {
                Titulo = $"Curso Idemp {Guid.NewGuid():N}",
                Descricao = "Desc",
                Categoria = "Tech",
                CargaHoraria = 20,
                DataCriacao = DateTime.UtcNow
            };
            context.Courses.Add(course);
            await context.SaveChangesAsync();

            var enrollment = new Enrollment
            {
                StudentId = student.StudentId,
                CourseId = course.CourseId,
                DataMatricula = DateTime.UtcNow,
                Status = false
            };
            context.Enrollments.Add(enrollment);
            await context.SaveChangesAsync();

            enrollmentId = enrollment.EnrollmentId;
        });

        var input = new CreatePaymentDto(enrollmentId, 100.00m);

        var response1 = await client.PostAsJsonAsync("/tech-curse/Payment", input);

        response1.StatusCode.Should().Be(HttpStatusCode.Created);

        var response2 = await client.PostAsJsonAsync("/tech-curse/Payment", input);

        response2.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
