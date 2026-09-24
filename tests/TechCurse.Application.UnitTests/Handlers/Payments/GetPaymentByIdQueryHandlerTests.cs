using FluentAssertions;
using Moq;
using TechCurse.Application.DTOs;
using TechCurse.Application.Features.Payments;
using TechCurse.Application.Features.Payments.Queries.GetPaymentById;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Entities;
using TechCurse.Domain.Enums;
using TechCurse.Domain.Exceptions;
using Xunit;

namespace TechCurse.Application.UnitTests.Handlers.Payments;

public class GetPaymentByIdQueryHandlerTests
{
    private readonly Mock<IPaymentRepository> _paymentRepositoryMock = new();
    private readonly Mock<ICacheService> _cacheServiceMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly GetPaymentByIdQueryHandler _handler;

    public GetPaymentByIdQueryHandlerTests()
    {
        _handler = new GetPaymentByIdQueryHandler(
            _paymentRepositoryMock.Object,
            _cacheServiceMock.Object,
            _currentUserServiceMock.Object
        );
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenPaymentNotFound_ShouldThrowNotFoundException()
    {
        _paymentRepositoryMock.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync((Payment?)null);

        var query = new GetPaymentByIdQuery(1);

        var act = () => _handler.Handle(query, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Pagamento não encontrado.");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenNonAdminAndMismatchedUser_ShouldThrowNotAllowedException()
    {
        var payment = new Payment
        {
            PaymentId = 1,
            Student = new Student { IdentityUserId = "user-123", Nome = "Student", Email = "s@s.com" }
        };

        _paymentRepositoryMock.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(payment);

        _currentUserServiceMock.Setup(u => u.GetUserId()).Returns("user-456");
        _currentUserServiceMock.Setup(u => u.IsInRole(UserRole.Admin)).Returns(false);

        var query = new GetPaymentByIdQuery(1);

        var act = () => _handler.Handle(query, CancellationToken.None);

        await act.Should().ThrowAsync<NotAllowedException>()
            .WithMessage("Você não possuí permissão suficiente para acessar este registro!");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenCacheHit_ShouldReturnCachedDto()
    {
        var payment = new Payment
        {
            PaymentId = 1,
            Student = new Student { IdentityUserId = "user-123", Nome = "Student", Email = "s@s.com" }
        };

        _paymentRepositoryMock.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(payment);

        _currentUserServiceMock.Setup(u => u.GetUserId()).Returns("user-123");
        _currentUserServiceMock.Setup(u => u.IsInRole(UserRole.Admin)).Returns(false);

        var cachedDto = new PaymentOutputDto(1, 10, 20, 100m, PaymentStatus.Paid, true, DateTime.UtcNow, DateTime.UtcNow, "TX_1", DadosDePagamento.CursoId, DadosDePagamento.CursoTitulo);
        _cacheServiceMock.Setup(c => c.GetAsync<PaymentOutputDto>($"{ChavesDeCachePagamento.Item}1"))
            .ReturnsAsync(cachedDto);

        var query = new GetPaymentByIdQuery(1);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Should().Be(cachedDto);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenCacheMissAndAdmin_ShouldReturnPaymentAndSetCache()
    {
        var payment = new Payment
        {
            PaymentId = 1,
            EnrollmentId = 10,
            StudentId = 20,
            Amount = 100m,
            Status = PaymentStatus.Pending,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            Student = new Student { IdentityUserId = "user-other", Nome = "Student", Email = "s@s.com" },
            Enrollment = DadosDePagamento.MatriculaComCurso(10, 20)
        };

        _paymentRepositoryMock.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(payment);

        _currentUserServiceMock.Setup(u => u.GetUserId()).Returns("admin-id");
        _currentUserServiceMock.Setup(u => u.IsInRole(UserRole.Admin)).Returns(true);

        _cacheServiceMock.Setup(c => c.GetAsync<PaymentOutputDto>($"{ChavesDeCachePagamento.Item}1"))
            .ReturnsAsync((PaymentOutputDto?)null);

        var query = new GetPaymentByIdQuery(1);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result.PaymentId.Should().Be(1);
        result.Amount.Should().Be(100m);
        result.CourseId.Should().Be(DadosDePagamento.CursoId);
        result.CourseTitulo.Should().Be(DadosDePagamento.CursoTitulo);

        _cacheServiceMock.Verify(c => c.SetAsync($"{ChavesDeCachePagamento.Item}1", It.IsAny<PaymentOutputDto>(), It.IsAny<TimeSpan>()), Times.Once);
    }
}
