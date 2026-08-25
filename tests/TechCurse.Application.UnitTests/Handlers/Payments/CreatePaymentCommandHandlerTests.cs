using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TechCurse.Application.Features.Payments.Commands.CreatePayment;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Entities;
using TechCurse.Domain.Enums;
using TechCurse.Domain.Exceptions;
using Xunit;

namespace TechCurse.Application.UnitTests.Handlers.Payments;

public class CreatePaymentCommandHandlerTests
{
    private readonly Mock<IEnrollmentRepository> _enrollmentRepositoryMock = new();
    private readonly Mock<IPaymentRepository> _paymentRepositoryMock = new();
    private readonly Mock<ICacheService> _cacheServiceMock = new();
    private readonly Mock<ILogger<CreatePaymentCommandHandler>> _loggerMock = new();
    private readonly CreatePaymentCommandHandler _handler;

    public CreatePaymentCommandHandlerTests()
    {
        _handler = new CreatePaymentCommandHandler(
            _enrollmentRepositoryMock.Object,
            _paymentRepositoryMock.Object,
            _cacheServiceMock.Object,
            _loggerMock.Object
        );
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenEnrollmentNotFound_ShouldThrowNotFoundException()
    {
        _enrollmentRepositoryMock.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync((Enrollment?)null);

        var command = new CreatePaymentCommand(1, 100m);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Matrícula não encontrada.");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenEnrollmentNotActive_ShouldThrowNotAllowedException()
    {
        var enrollment = new Enrollment { EnrollmentId = 1, StudentId = 2, CourseId = 3 };
        _enrollmentRepositoryMock.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(enrollment);
        _enrollmentRepositoryMock.Setup(r => r.EnrollmentIsActiveAsync(1))
            .ReturnsAsync(false);

        var command = new CreatePaymentCommand(1, 100m);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotAllowedException>()
            .WithMessage("Não é possível criar um pagamento para uma matrícula inativa.");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenActivePaymentAlreadyExists_ShouldThrowConflictException()
    {
        var enrollment = new Enrollment { EnrollmentId = 1, StudentId = 2, CourseId = 3 };
        _enrollmentRepositoryMock.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(enrollment);
        _enrollmentRepositoryMock.Setup(r => r.EnrollmentIsActiveAsync(1))
            .ReturnsAsync(true);
        _paymentRepositoryMock.Setup(r => r.ExistsActiveByEnrollmentAsync(1))
            .ReturnsAsync(true);

        var command = new CreatePaymentCommand(1, 100m);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("Já existe um pagamento ativo para esta matrícula.");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenValid_ShouldCreatePaymentAndClearCaches()
    {
        var enrollment = new Enrollment { EnrollmentId = 1, StudentId = 2, CourseId = 3 };
        _enrollmentRepositoryMock.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(enrollment);
        _enrollmentRepositoryMock.Setup(r => r.EnrollmentIsActiveAsync(1))
            .ReturnsAsync(true);
        _paymentRepositoryMock.Setup(r => r.ExistsActiveByEnrollmentAsync(1))
            .ReturnsAsync(false);

        _paymentRepositoryMock.Setup(r => r.AddAsync(It.IsAny<Payment>()))
            .Callback<Payment>(p => p.PaymentId = 10)
            .Returns(Task.CompletedTask);

        var command = new CreatePaymentCommand(1, 150m);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.PaymentId.Should().Be(10);
        result.EnrollmentId.Should().Be(1);
        result.StudentId.Should().Be(2);
        result.Amount.Should().Be(150m);
        result.Status.Should().Be(PaymentStatus.Pending);

        _paymentRepositoryMock.Verify(r => r.AddAsync(It.Is<Payment>(p => p.Amount == 150m && p.Status == PaymentStatus.Pending)), Times.Once);
        _cacheServiceMock.Verify(c => c.RemoveByPrefixAsync("payments:list:"), Times.Once);
        _cacheServiceMock.Verify(c => c.RemoveByPrefixAsync("payments:item:"), Times.Once);
    }
}
