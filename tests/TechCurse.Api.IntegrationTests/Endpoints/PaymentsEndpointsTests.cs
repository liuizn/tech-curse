using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using TechCurse.Api.IntegrationTests.Fixtures;
using TechCurse.Application.DTOs;
using TechCurse.Domain.Entities;
using TechCurse.Domain.Enums;
using Xunit;

namespace TechCurse.Api.IntegrationTests.Endpoints;

public class PaymentsEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PaymentsEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Get_WhenUnauthenticated_ShouldReturn401Unauthorized()
    {
        var client = _factory.CreateAnonymousClient();

        var response = await client.GetAsync("/tech-curse/Payment");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Post_WhenMissingIdempotencyKeyHeader_ShouldReturn400BadRequest()
    {
        var client = _factory.CreateAdminClient();
        var input = new CreatePaymentDto(1, 100m);

        var response = await client.PostAsJsonAsync("/tech-curse/Payment", input);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Post_WhenStudentCallsAdminPaymentEndpoint_ShouldReturn403Forbidden()
    {
        var client = _factory.CreateStudentClient();
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var input = new CreatePaymentDto(1, 100m);

        var response = await client.PostAsJsonAsync("/tech-curse/Payment", input);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Post_WhenValid_ShouldReturn201Created()
    {
        var client = _factory.CreateAdminClient();
        var idempotencyKey = Guid.NewGuid().ToString();
        client.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);

        var userId = $"pmt-user-{Guid.NewGuid():N}";
        var email = $"pmt_{Guid.NewGuid():N}@techcurse.com";

        int enrollmentId = 0;
        await _factory.ExecuteDbContextAsync(async context =>
        {
            var user = new IdentityUser { Id = userId, Email = email, UserName = email, NormalizedEmail = email.ToUpperInvariant(), NormalizedUserName = email.ToUpperInvariant() };
            context.Users.Add(user);

            var student = new Student
            {
                Nome = "Aluno Pagamento",
                Email = email,
                IdentityUserId = userId,
                IsDeleted = false,
                DataCadastro = DateTime.UtcNow
            };
            context.Students.Add(student);

            var course = new Course
            {
                Titulo = $"Curso Pagamento {Guid.NewGuid():N}",
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
                Status = true
            };
            context.Enrollments.Add(enrollment);
            await context.SaveChangesAsync();

            enrollmentId = enrollment.EnrollmentId;
        });

        var input = new CreatePaymentDto(enrollmentId, 250.00m);

        var response = await client.PostAsJsonAsync("/tech-curse/Payment", input);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Post_WhenEnrollmentInactive_ShouldReturn409Conflict()
    {
        var client = _factory.CreateAdminClient();
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var userId = $"pmt-inativa-{Guid.NewGuid():N}";
        var email = $"pmt_inativa_{Guid.NewGuid():N}@techcurse.com";

        int enrollmentId = 0;
        await _factory.ExecuteDbContextAsync(async context =>
        {
            var user = new IdentityUser { Id = userId, Email = email, UserName = email, NormalizedEmail = email.ToUpperInvariant(), NormalizedUserName = email.ToUpperInvariant() };
            context.Users.Add(user);

            var student = new Student
            {
                Nome = "Aluno Matrícula Inativa",
                Email = email,
                IdentityUserId = userId,
                IsDeleted = false,
                DataCadastro = DateTime.UtcNow
            };
            context.Students.Add(student);

            var course = new Course
            {
                Titulo = $"Curso Matrícula Inativa {Guid.NewGuid():N}",
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

        var input = new CreatePaymentDto(enrollmentId, 250.00m);

        var response = await client.PostAsJsonAsync("/tech-curse/Payment", input);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Post_WhenEnrollmentCreatedByStudent_ShouldReturn201Created()
    {
        var email = $"pmt_fluxo_{Guid.NewGuid():N}@techcurse.com";
        var userId = $"pmt-fluxo-{Guid.NewGuid():N}";

        int courseId = 0;
        int studentId = 0;
        await _factory.ExecuteDbContextAsync(async context =>
        {
            var user = new IdentityUser { Id = userId, Email = email, UserName = email, NormalizedEmail = email.ToUpperInvariant(), NormalizedUserName = email.ToUpperInvariant() };
            context.Users.Add(user);

            var student = new Student
            {
                Nome = "Aluno Fluxo",
                Email = email,
                IdentityUserId = userId,
                IsDeleted = false,
                DataCadastro = DateTime.UtcNow
            };
            context.Students.Add(student);

            var course = new Course
            {
                Titulo = $"Curso Fluxo {Guid.NewGuid():N}",
                Descricao = "Desc",
                Categoria = "Tech",
                CargaHoraria = 20,
                DataCriacao = DateTime.UtcNow
            };
            context.Courses.Add(course);
            await context.SaveChangesAsync();

            courseId = course.CourseId;
            studentId = student.StudentId;
        });

        var studentClient = _factory.CreateStudentClient(email, userId);
        var enrollResponse = await studentClient.PostAsJsonAsync("/tech-curse/Enrollment", new EnrollmentInputDto(courseId, studentId));
        enrollResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        int enrollmentId = 0;
        await _factory.ExecuteDbContextAsync(context =>
        {
            enrollmentId = context.Enrollments.Single(e => e.CourseId == courseId).EnrollmentId;
            return Task.CompletedTask;
        });

        var adminClient = _factory.CreateAdminClient();
        adminClient.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var response = await adminClient.PostAsJsonAsync("/tech-curse/Payment", new CreatePaymentDto(enrollmentId, 250.00m));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetById_WhenNotFound_ShouldReturn404NotFound()
    {
        var client = _factory.CreateAdminClient();

        var response = await client.GetAsync("/tech-curse/Payment/99999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Process_WhenValid_ShouldReturn200OK()
    {
        var client = _factory.CreateAdminClient();
        var idempotencyKey = Guid.NewGuid().ToString();
        client.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);

        var userId = $"proc-user-{Guid.NewGuid():N}";
        var email = $"proc_{Guid.NewGuid():N}@techcurse.com";

        int paymentId = 0;
        await _factory.ExecuteDbContextAsync(async context =>
        {
            var user = new IdentityUser { Id = userId, Email = email, UserName = email, NormalizedEmail = email.ToUpperInvariant(), NormalizedUserName = email.ToUpperInvariant() };
            context.Users.Add(user);

            var student = new Student
            {
                Nome = "Aluno Process",
                Email = email,
                IdentityUserId = userId,
                IsDeleted = false,
                DataCadastro = DateTime.UtcNow
            };
            context.Students.Add(student);

            var course = new Course
            {
                Titulo = $"Curso Proc {Guid.NewGuid():N}",
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

            var payment = new Payment
            {
                EnrollmentId = enrollment.EnrollmentId,
                StudentId = student.StudentId,
                Amount = 199.90m,
                Status = PaymentStatus.Pending,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            context.Payments.Add(payment);
            await context.SaveChangesAsync();

            paymentId = payment.PaymentId;
        });

        var input = new ProcessPaymentDto(paymentId, PaymentMethodType.CreditCard);

        var response = await client.PostAsJsonAsync("/tech-curse/Payment/process", input);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ProcessPaymentOutputDto>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.ExternalTransactionId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Refund_WhenValid_ShouldReturn200OK()
    {
        var client = _factory.CreateAdminClient();
        var idempotencyKey = Guid.NewGuid().ToString();
        client.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);

        var userId = $"ref-user-{Guid.NewGuid():N}";
        var email = $"ref_{Guid.NewGuid():N}@techcurse.com";

        int paymentId = 0;
        await _factory.ExecuteDbContextAsync(async context =>
        {
            var user = new IdentityUser { Id = userId, Email = email, UserName = email, NormalizedEmail = email.ToUpperInvariant(), NormalizedUserName = email.ToUpperInvariant() };
            context.Users.Add(user);

            var student = new Student
            {
                Nome = "Aluno Refund",
                Email = email,
                IdentityUserId = userId,
                IsDeleted = false,
                DataCadastro = DateTime.UtcNow
            };
            context.Students.Add(student);

            var course = new Course
            {
                Titulo = $"Curso Refund {Guid.NewGuid():N}",
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

            var payment = new Payment
            {
                EnrollmentId = enrollment.EnrollmentId,
                StudentId = student.StudentId,
                Amount = 199.90m,
                Status = PaymentStatus.Paid,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                PaidAt = DateTime.UtcNow,
                ExternalTransactionId = "TX_SIMULATED_12345"
            };
            context.Payments.Add(payment);
            await context.SaveChangesAsync();

            paymentId = payment.PaymentId;
        });

        var input = new RefundPaymentDto(paymentId, "Cancelamento solicitado");

        var response = await client.PostAsJsonAsync("/tech-curse/Payment/refund", input);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<RefundPaymentOutputDto>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Consultas_ShouldIncluirCursoDoPagamento()
    {
        var userId = $"curso-pag-{Guid.NewGuid():N}";
        var email = $"curso_pag_{Guid.NewGuid():N}@techcurse.com";
        var titulo = $"Curso do Pagamento {Guid.NewGuid():N}";
        int paymentId = 0, studentId = 0, enrollmentId = 0, courseId = 0;
        await _factory.ExecuteDbContextAsync(async context =>
        {
            var user = new IdentityUser { Id = userId, Email = email, UserName = $"cursopag{Guid.NewGuid():N}", NormalizedEmail = email.ToUpperInvariant() };
            context.Users.Add(user);
            var student = new Student { Nome = "Aluno Curso Pagamento", Email = email, IdentityUserId = userId, IsDeleted = false, DataCadastro = DateTime.UtcNow };
            context.Students.Add(student);
            var course = new Course { Titulo = titulo, Descricao = "Desc", Categoria = "Tech", CargaHoraria = 20, DataCriacao = DateTime.UtcNow };
            context.Courses.Add(course);
            await context.SaveChangesAsync();
            var enrollment = new Enrollment { StudentId = student.StudentId, CourseId = course.CourseId, DataMatricula = DateTime.UtcNow, Status = true };
            context.Enrollments.Add(enrollment);
            await context.SaveChangesAsync();
            var payment = new Payment { EnrollmentId = enrollment.EnrollmentId, StudentId = student.StudentId, Amount = 99.90m, Status = PaymentStatus.Pending, IsActive = true, CreatedAt = DateTime.UtcNow };
            context.Payments.Add(payment);
            await context.SaveChangesAsync();
            paymentId = payment.PaymentId;
            studentId = student.StudentId;
            enrollmentId = enrollment.EnrollmentId;
            courseId = course.CourseId;
        });
        var admin = _factory.CreateAdminClient();
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        var porId = await admin.GetFromJsonAsync<PaymentOutputDto>($"/tech-curse/Payment/{paymentId}", jsonOptions);
        var porEstudante = await admin.GetFromJsonAsync<PagedResultDto<PaymentOutputDto>>($"/tech-curse/Payment/student/{studentId}", jsonOptions);
        var porMatricula = await admin.GetFromJsonAsync<List<PaymentOutputDto>>($"/tech-curse/Payment/enrollment/{enrollmentId}", jsonOptions);

        porId!.CourseId.Should().Be(courseId);
        porId.CourseTitulo.Should().Be(titulo);
        porEstudante!.Items.Should().ContainSingle(p => p.PaymentId == paymentId && p.CourseId == courseId && p.CourseTitulo == titulo);
        porMatricula.Should().ContainSingle(p => p.PaymentId == paymentId && p.CourseId == courseId && p.CourseTitulo == titulo);
    }
}
