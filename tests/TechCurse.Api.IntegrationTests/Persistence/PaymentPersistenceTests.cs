using Microsoft.EntityFrameworkCore;
using TechCurse.Domain.Entities;
using TechCurse.Domain.Enums;
using TechCurse.Infrastructure.Data;
using TechCurse.Infrastructure.Repositories;

namespace TechCurse.Api.IntegrationTests.Persistence;

public class PaymentPersistenceTests
{
    private DbContextOptions<TechCurseContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<TechCurseContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void AdicionarPagamento_DeveAdicionarPagamentoComSucesso()
    {
        var options = CreateOptions();
        using var context = new TechCurseContext(options);
        var payment = new Payment
        {
            EnrollmentId = 1,
            StudentId = 1,
            Amount = 100.00m,
            Status = PaymentStatus.Pending
        };

        context.Payments.Add(payment);
        context.SaveChanges();

        var savedPayment = context.Payments.FirstOrDefault(p => p.PaymentId == payment.PaymentId);
        Assert.NotNull(savedPayment);
        Assert.Equal(payment.EnrollmentId, savedPayment.EnrollmentId);
        Assert.Equal(payment.StudentId, savedPayment.StudentId);
        Assert.Equal(payment.Amount, savedPayment.Amount);
        Assert.Equal(payment.Status, savedPayment.Status);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void AtualizarStatusPagamento_DeveAtualizarStatusComSucesso()
    {
        var options = CreateOptions();
        using var context = new TechCurseContext(options);
        var payment = new Payment
        {
            EnrollmentId = 1,
            StudentId = 1,
            Amount = 100.00m,
            Status = PaymentStatus.Pending
        };
        context.Payments.Add(payment);
        context.SaveChanges();

        payment.Status = PaymentStatus.Paid;
        context.Payments.Update(payment);
        context.SaveChanges();

        var updatedPayment = context.Payments.FirstOrDefault(p => p.PaymentId == payment.PaymentId);
        Assert.NotNull(updatedPayment);
        Assert.Equal(PaymentStatus.Paid, updatedPayment.Status);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void EstadosPagamento_DevePermitirTransicoesDePendingParaPaidFailedRefunded()
    {
        var options = CreateOptions();
        using var context = new TechCurseContext(options);
        var payment = new Payment
        {
            EnrollmentId = 2,
            StudentId = 2,
            Amount = 50.00m,
            Status = PaymentStatus.Pending
        };
        context.Payments.Add(payment);
        context.SaveChanges();

        payment.Status = PaymentStatus.Paid;
        context.Payments.Update(payment);
        context.SaveChanges();
        var p1 = context.Payments.First(p => p.PaymentId == payment.PaymentId);
        Assert.Equal(PaymentStatus.Paid, p1.Status);

        payment.Status = PaymentStatus.Failed;
        context.Payments.Update(payment);
        context.SaveChanges();
        var p2 = context.Payments.First(p => p.PaymentId == payment.PaymentId);
        Assert.Equal(PaymentStatus.Failed, p2.Status);

        payment.Status = PaymentStatus.Refunded;
        context.Payments.Update(payment);
        context.SaveChanges();
        var p3 = context.Payments.First(p => p.PaymentId == payment.PaymentId);
        Assert.Equal(PaymentStatus.Refunded, p3.Status);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Idempotencia_AtualizarMesmoStatusDiversasVezes_NaoCriaDuplicatas()
    {
        var options = CreateOptions();
        using var context = new TechCurseContext(options);
        var payment = new Payment
        {
            EnrollmentId = 4,
            StudentId = 4,
            Amount = 75.00m,
            Status = PaymentStatus.Pending
        };
        context.Payments.Add(payment);
        context.SaveChanges();

        payment.Status = PaymentStatus.Paid;
        context.Payments.Update(payment);
        context.SaveChanges();

        payment.Status = PaymentStatus.Paid;
        context.Payments.Update(payment);
        context.SaveChanges();

        var payments = context.Payments.Where(p => p.EnrollmentId == payment.EnrollmentId).ToList();
        Assert.Single(payments);
        Assert.Equal(PaymentStatus.Paid, payments[0].Status);
        Assert.Equal(payment.PaymentId, payments[0].PaymentId);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AtualizarPagamento_NaoDeveAnexarNavegacoesDeMatriculaCursoEEstudante()
    {
        var databaseName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<TechCurseContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        int paymentId;
        await using (var seedContext = new TechCurseContext(options))
        {
            var student = new Student
            {
                Nome = "Aluno Persistencia",
                Email = "aluno.persistencia@techcurse.com",
                IdentityUserId = "persistencia-user"
            };
            var course = new Course
            {
                Titulo = "Curso Persistencia",
                Descricao = "Desc",
                Categoria = "Tech",
                CargaHoraria = 10
            };
            seedContext.Students.Add(student);
            seedContext.Courses.Add(course);
            await seedContext.SaveChangesAsync();

            var enrollment = new Enrollment
            {
                StudentId = student.StudentId,
                CourseId = course.CourseId,
                DataMatricula = DateTime.UtcNow,
                Status = true
            };
            seedContext.Enrollments.Add(enrollment);
            await seedContext.SaveChangesAsync();

            var payment = new Payment
            {
                EnrollmentId = enrollment.EnrollmentId,
                StudentId = student.StudentId,
                Amount = 100.00m,
                Status = PaymentStatus.Pending,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            seedContext.Payments.Add(payment);
            await seedContext.SaveChangesAsync();
            paymentId = payment.PaymentId;
        }

        await using var readContext = new TechCurseContext(options);
        var repository = new PaymentRepository(readContext);

        var payment2 = await repository.GetByIdAsync(paymentId);
        Assert.NotNull(payment2);

        payment2!.Status = PaymentStatus.Paid;
        await repository.UpdateAsync(payment2);

        Assert.Empty(readContext.ChangeTracker.Entries<Course>());
        Assert.Empty(readContext.ChangeTracker.Entries<Enrollment>());
        Assert.Empty(readContext.ChangeTracker.Entries<Student>());

        await using var verifyContext = new TechCurseContext(options);
        var reloaded = await verifyContext.Payments.FirstAsync(p => p.PaymentId == paymentId);
        Assert.Equal(PaymentStatus.Paid, reloaded.Status);
    }
}
