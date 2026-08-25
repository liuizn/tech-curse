using System.Net;
using System.Net.Http.Json;
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
        // Arrange
        var client = _factory.CreateAnonymousClient();

        // Act
        var response = await client.GetAsync("/tech-curse/Payment");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Post_WhenMissingIdempotencyKeyHeader_ShouldReturn400BadRequest()
    {
        // Arrange
        var client = _factory.CreateAdminClient();
        var input = new CreatePaymentDto(1, 100m);

        // Act (no Idempotency-Key header)
        var response = await client.PostAsJsonAsync("/tech-curse/Payment", input);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Post_WhenStudentCallsAdminPaymentEndpoint_ShouldReturn403Forbidden()
    {
        // Arrange
        var client = _factory.CreateStudentClient();
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var input = new CreatePaymentDto(1, 100m);

        // Act
        var response = await client.PostAsJsonAsync("/tech-curse/Payment", input);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Post_WhenValid_ShouldReturn201Created()
    {
        // Arrange
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
                Status = false
            };
            context.Enrollments.Add(enrollment);
            await context.SaveChangesAsync();

            enrollmentId = enrollment.EnrollmentId;
        });

        var input = new CreatePaymentDto(enrollmentId, 250.00m);

        // Act
        var response = await client.PostAsJsonAsync("/tech-curse/Payment", input);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetById_WhenNotFound_ShouldReturn404NotFound()
    {
        // Arrange
        var client = _factory.CreateAdminClient();

        // Act
        var response = await client.GetAsync("/tech-curse/Payment/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Process_WhenValid_ShouldReturn200OK()
    {
        // Arrange
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

        // Act
        var response = await client.PostAsJsonAsync("/tech-curse/Payment/process", input);

        // Assert
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
        // Arrange
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

        // Act
        var response = await client.PostAsJsonAsync("/tech-curse/Payment/refund", input);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<RefundPaymentOutputDto>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
    }
}
