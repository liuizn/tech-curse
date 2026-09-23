using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using TechCurse.Api.IntegrationTests.Fixtures;
using TechCurse.Application.DTOs;
using TechCurse.Domain.Entities;
using Xunit;

namespace TechCurse.Api.IntegrationTests.Endpoints;

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
        var client = _factory.CreateAnonymousClient();

        var response = await client.GetAsync("/tech-curse/Student");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetAll_WhenUserIsStudent_ShouldReturn403Forbidden()
    {
        var client = _factory.CreateStudentClient();

        var response = await client.GetAsync("/tech-curse/Student");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetAll_WhenUserIsAdmin_ShouldReturn200OK()
    {
        var client = _factory.CreateAdminClient();

        var response = await client.GetAsync("/tech-curse/Student?pageNumber=1&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetById_WhenStudentNotFound_ShouldReturn404NotFound()
    {
        var client = _factory.CreateAdminClient();

        var response = await client.GetAsync("/tech-curse/Student/99999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetSelf_WhenStudentAuthenticated_ShouldReturn200OK()
    {
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

        var response = await client.GetAsync("/tech-curse/Student/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var studentDto = await response.Content.ReadFromJsonAsync<StudentOutputDto>();
        studentDto.Should().NotBeNull();
        studentDto!.Email.Should().Be(email);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Post_WhenPayloadInvalid_ShouldReturn422UnprocessableEntity()
    {
        var client = _factory.CreateAdminClient();
        var input = new StudentPostDto("", "invalid-email");

        var response = await client.PostAsJsonAsync("/tech-curse/Student", input);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Post_WhenValidAndUserExists_ShouldReturn201Created()
    {
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

        var response = await client.PostAsJsonAsync("/tech-curse/Student", input);

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

        var response = await client.PutAsJsonAsync($"/tech-curse/Student/{studentId}", input);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Delete_WhenAdminDeletesStudent_ShouldReturn204NoContent()
    {
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

        var response = await client.DeleteAsync($"/tech-curse/Student/{studentId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetEnrollments_WhenAdmin_ShouldIncluirEnrollmentId()
    {
        var studentId = 0;
        var enrollmentId = 0;
        var courseId = 0;
        await _factory.ExecuteDbContextAsync(async context =>
        {
            var usuario = new IdentityUser { Id = $"matriculas-{Guid.NewGuid():N}", UserName = $"matriculas{Guid.NewGuid():N}", Email = $"matriculas_{Guid.NewGuid():N}@techcurse.com" };
            context.Users.Add(usuario);
            var aluno = new Student { Nome = "Aluno Matriculas", Email = usuario.Email!, IdentityUserId = usuario.Id, DataCadastro = DateTime.UtcNow, IsDeleted = false };
            context.Students.Add(aluno);
            var curso = new Course { Titulo = $"Curso Matriculas {Guid.NewGuid():N}", Descricao = "Desc", Categoria = "Tech", CargaHoraria = 10, DataCriacao = DateTime.UtcNow };
            context.Courses.Add(curso);
            await context.SaveChangesAsync();
            var matricula = new Enrollment { StudentId = aluno.StudentId, CourseId = curso.CourseId, DataMatricula = DateTime.UtcNow, Status = true };
            context.Enrollments.Add(matricula);
            await context.SaveChangesAsync();
            studentId = aluno.StudentId;
            enrollmentId = matricula.EnrollmentId;
            courseId = curso.CourseId;
        });
        var admin = _factory.CreateAdminClient();

        var resposta = await admin.GetAsync($"/tech-curse/Student/{studentId}/enrollments");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var matriculas = await resposta.Content.ReadFromJsonAsync<List<CourseStudentOutputDto>>();
        matriculas.Should().ContainSingle();
        matriculas![0].EnrollmentId.Should().Be(enrollmentId);
        matriculas[0].CourseId.Should().Be(courseId);
    }
}
