using FluentAssertions;
using Moq;
using TechCurse.Application.DTOs;
using TechCurse.Application.Features.Payments;
using TechCurse.Application.Features.Payments.Queries.GetPaymentsByEnrollmentId;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Entities;
using TechCurse.Domain.Enums;
using TechCurse.Domain.Exceptions;
using Xunit;

namespace TechCurse.Application.UnitTests.Handlers.Payments;

public class GetPaymentsByEnrollmentIdQueryHandlerTests
{
    private readonly Mock<IPaymentRepository> _paymentRepositoryMock = new();
    private readonly Mock<ICacheService> _cacheServiceMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly GetPaymentsByEnrollmentIdQueryHandler _handler;

    public GetPaymentsByEnrollmentIdQueryHandlerTests()
    {
        _handler = new GetPaymentsByEnrollmentIdQueryHandler(
            _paymentRepositoryMock.Object,
            _cacheServiceMock.Object,
            _currentUserServiceMock.Object
        );
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenEnrollmentHasNoPaymentsOrNoStudent_ShouldThrowNotFoundException()
    {
        _paymentRepositoryMock.Setup(r => r.GetByEnrollmentIdAsync(1))
            .ReturnsAsync(new List<Payment>());

        var query = new GetPaymentsByEnrollmentIdQuery(1);

        var act = () => _handler.Handle(query, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Matrícula não encontrada ou sem estudante associado.");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenNonAdminAndMismatchUser_ShouldThrowNotAllowedException()
    {
        var payments = new List<Payment>
        {
            new()
            {
                PaymentId = 1,
                Enrollment = new Enrollment
                {
                    Student = new Student { IdentityUserId = "student-id" }
                }
            }
        };

        _paymentRepositoryMock.Setup(r => r.GetByEnrollmentIdAsync(1))
            .ReturnsAsync(payments);

        _currentUserServiceMock.Setup(u => u.GetUserId()).Returns("other-id");
        _currentUserServiceMock.Setup(u => u.IsInRole(UserRole.Admin)).Returns(false);

        var query = new GetPaymentsByEnrollmentIdQuery(1);

        var act = () => _handler.Handle(query, CancellationToken.None);

        await act.Should().ThrowAsync<NotAllowedException>()
            .WithMessage("Você não possuí permissão suficiente para acessar este registro!");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenValidAndCacheHit_ShouldReturnCachedPayments()
    {
        var payments = new List<Payment>
        {
            new()
            {
                PaymentId = 1,
                Enrollment = new Enrollment
                {
                    Student = new Student { IdentityUserId = "student-id" }
                }
            }
        };

        _paymentRepositoryMock.Setup(r => r.GetByEnrollmentIdAsync(1))
            .ReturnsAsync(payments);

        _currentUserServiceMock.Setup(u => u.GetUserId()).Returns("student-id");
        _currentUserServiceMock.Setup(u => u.IsInRole(UserRole.Admin)).Returns(false);

        var cachedDtos = new List<PaymentOutputDto>
        {
            new(1, 1, 2, 100m, PaymentStatus.Paid, true, DateTime.UtcNow, null, null, DadosDePagamento.CursoId, DadosDePagamento.CursoTitulo)
        };

        _cacheServiceMock.Setup(c => c.GetAsync<IEnumerable<PaymentOutputDto>>($"{ChavesDeCachePagamento.PorMatricula}1"))
            .ReturnsAsync(cachedDtos);

        var query = new GetPaymentsByEnrollmentIdQuery(1);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Should().BeEquivalentTo(cachedDtos);
    }
}
