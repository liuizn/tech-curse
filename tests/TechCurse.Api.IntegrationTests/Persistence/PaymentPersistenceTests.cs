using Microsoft.EntityFrameworkCore;
using TechCurse.Domain.Entities;
using TechCurse.Domain.Enums;
using TechCurse.Infrastructure.Data;

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
        // Arrange
        var options = CreateOptions();
        using var context = new TechCurseContext(options);
        var payment = new Payment
        {
            EnrollmentId = 1,
            StudentId = 1,
            Amount = 100.00m,
            Status = PaymentStatus.Pending
        };

        // Act
        context.Payments.Add(payment);
        context.SaveChanges();

        // Assert
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
        // Arrange
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

        // Act
        payment.Status = PaymentStatus.Paid;
        context.Payments.Update(payment);
        context.SaveChanges();

        // Assert
        var updatedPayment = context.Payments.FirstOrDefault(p => p.PaymentId == payment.PaymentId);
        Assert.NotNull(updatedPayment);
        Assert.Equal(PaymentStatus.Paid, updatedPayment.Status);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void EstadosPagamento_DevePermitirTransicoesDePendingParaPaidFailedRefunded()
    {
        // Arrange
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

        // Act & Assert: Pending -> Paid
        payment.Status = PaymentStatus.Paid;
        context.Payments.Update(payment);
        context.SaveChanges();
        var p1 = context.Payments.First(p => p.PaymentId == payment.PaymentId);
        Assert.Equal(PaymentStatus.Paid, p1.Status);

        // Act & Assert: Paid -> Failed
        payment.Status = PaymentStatus.Failed;
        context.Payments.Update(payment);
        context.SaveChanges();
        var p2 = context.Payments.First(p => p.PaymentId == payment.PaymentId);
        Assert.Equal(PaymentStatus.Failed, p2.Status);

        // Act & Assert: Failed -> Refunded
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
        // Arrange
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

        // Act: aplicar a mesma transição várias vezes
        payment.Status = PaymentStatus.Paid;
        context.Payments.Update(payment);
        context.SaveChanges();

        payment.Status = PaymentStatus.Paid; // mesma operação novamente
        context.Payments.Update(payment);
        context.SaveChanges();

        // Assert: somente um registro existe e status é Paid
        var payments = context.Payments.Where(p => p.EnrollmentId == payment.EnrollmentId).ToList();
        Assert.Single(payments);
        Assert.Equal(PaymentStatus.Paid, payments[0].Status);
        Assert.Equal(payment.PaymentId, payments[0].PaymentId);
    }
}
