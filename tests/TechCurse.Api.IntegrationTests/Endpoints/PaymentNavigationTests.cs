using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TechCurse.Api.IntegrationTests.Fixtures;
using TechCurse.Domain.Entities;
using TechCurse.Domain.Enums;
using TechCurse.Infrastructure.Data;
using Xunit;

namespace TechCurse.Api.IntegrationTests.Endpoints;

public class PaymentNavigationTests : IClassFixture<CustomWebApplicationFactory>
{
    private const int CourseId = 9100;
    private const int StudentAtivoId = 9101;
    private const int StudentRemovidoId = 9102;
    private const int EnrollmentAtivaId = 9103;
    private const int EnrollmentRemovidaId = 9104;
    private const int PaymentAtivoId = 9105;
    private const int PaymentRemovidoId = 9106;

    private readonly CustomWebApplicationFactory _factory;

    public PaymentNavigationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        SemearAsync().GetAwaiter().GetResult();
    }

    private async Task SemearAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TechCurseContext>();

        if (await context.Payments.FindAsync(PaymentAtivoId) is not null)
        {
            return;
        }

        context.Courses.Add(new Course
        {
            CourseId = CourseId,
            Titulo = "Curso de Navegacao",
            Descricao = "Curso usado pelos testes de navegacao de pagamento",
            Categoria = "Testes",
            CargaHoraria = 10
        });

        context.Students.AddRange(
            new Student
            {
                StudentId = StudentAtivoId,
                Nome = "Aluno Ativo",
                Email = "aluno.ativo.navegacao@techcurse.com",
                IdentityUserId = "navegacao-ativo",
                IsDeleted = false
            },
            new Student
            {
                StudentId = StudentRemovidoId,
                Nome = "Aluno Removido",
                Email = "aluno.removido.navegacao@techcurse.com",
                IdentityUserId = "navegacao-removido",
                IsDeleted = true
            });

        context.Enrollments.AddRange(
            new Enrollment { EnrollmentId = EnrollmentAtivaId, StudentId = StudentAtivoId, CourseId = CourseId },
            new Enrollment { EnrollmentId = EnrollmentRemovidaId, StudentId = StudentRemovidoId, CourseId = CourseId });

        context.Payments.AddRange(
            new Payment
            {
                PaymentId = PaymentAtivoId,
                StudentId = StudentAtivoId,
                EnrollmentId = EnrollmentAtivaId,
                Amount = 100m,
                Status = PaymentStatus.Pending,
                IsActive = true
            },
            new Payment
            {
                PaymentId = PaymentRemovidoId,
                StudentId = StudentRemovidoId,
                EnrollmentId = EnrollmentRemovidaId,
                Amount = 200m,
                Status = PaymentStatus.Pending,
                IsActive = true
            });

        await context.SaveChangesAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetById_QuandoPagamentoExiste_NaoDeveFalharAoLerNavegacaoStudent()
    {
        var client = _factory.CreateAdminClient();

        var response = await client.GetAsync($"/tech-curse/Payment/{PaymentAtivoId}");

        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetById_QuandoAlunoEstaSoftDeleted_DeveContinuarRetornandoOPagamento()
    {
        var client = _factory.CreateAdminClient();

        var response = await client.GetAsync($"/tech-curse/Payment/{PaymentRemovidoId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetByEnrollment_QuandoExistemPagamentos_NaoDeveFalharAoLerNavegacaoEnrollmentStudent()
    {
        var client = _factory.CreateAdminClient();

        var response = await client.GetAsync($"/tech-curse/Payment/enrollment/{EnrollmentAtivaId}");

        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetByEnrollment_QuandoAlunoEstaSoftDeleted_DeveContinuarRetornandoOsPagamentos()
    {
        var client = _factory.CreateAdminClient();

        var response = await client.GetAsync($"/tech-curse/Payment/enrollment/{EnrollmentRemovidaId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
