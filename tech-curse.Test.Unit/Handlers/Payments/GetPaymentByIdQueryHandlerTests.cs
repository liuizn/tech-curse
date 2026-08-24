using FluentAssertions;
using Moq;
using TechCurse.src.Application.DTOs;
using TechCurse.src.Application.Features.Payments.Queries.GetPaymentById;
using TechCurse.src.Application.Interfaces;
using TechCurse.src.Domain.Entities;
using TechCurse.src.Domain.Enums;
using TechCurse.src.Domain.Exceptions;
using Xunit;

namespace TechCurse.Test.Unit.Handlers.Payments;

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
        // Arrange
        _paymentRepositoryMock.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync((Payment?)null);

        var query = new GetPaymentByIdQuery(1);

        // Act
        var act = () => _handler.Handle(query, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Pagamento não encontrado.");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenNonAdminAndMismatchedUser_ShouldThrowNotAllowedException()
    {
        // Arrange
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

        // Act
        var act = () => _handler.Handle(query, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotAllowedException>()
            .WithMessage("Você não possuí permissão suficiente para acessar este registro!");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenCacheHit_ShouldReturnCachedDto()
    {
        // Arrange
        var payment = new Payment
        {
            PaymentId = 1,
            Student = new Student { IdentityUserId = "user-123", Nome = "Student", Email = "s@s.com" }
        };

        _paymentRepositoryMock.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(payment);

        _currentUserServiceMock.Setup(u => u.GetUserId()).Returns("user-123");
        _currentUserServiceMock.Setup(u => u.IsInRole(UserRole.Admin)).Returns(false);

        var cachedDto = new PaymentOutputDto(1, 10, 20, 100m, PaymentStatus.Paid, true, DateTime.UtcNow, DateTime.UtcNow, "TX_1");
        _cacheServiceMock.Setup(c => c.GetAsync<PaymentOutputDto>("payments:item:1"))
            .ReturnsAsync(cachedDto);

        var query = new GetPaymentByIdQuery(1);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().Be(cachedDto);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenCacheMissAndAdmin_ShouldReturnPaymentAndSetCache()
    {
        // Arrange
        var payment = new Payment
        {
            PaymentId = 1,
            EnrollmentId = 10,
            StudentId = 20,
            Amount = 100m,
            Status = PaymentStatus.Pending,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            Student = new Student { IdentityUserId = "user-other", Nome = "Student", Email = "s@s.com" }
        };

        _paymentRepositoryMock.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(payment);

        _currentUserServiceMock.Setup(u => u.GetUserId()).Returns("admin-id");
        _currentUserServiceMock.Setup(u => u.IsInRole(UserRole.Admin)).Returns(true);

        _cacheServiceMock.Setup(c => c.GetAsync<PaymentOutputDto>("payments:item:1"))
            .ReturnsAsync((PaymentOutputDto?)null);

        var query = new GetPaymentByIdQuery(1);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.PaymentId.Should().Be(1);
        result.Amount.Should().Be(100m);

        _cacheServiceMock.Verify(c => c.SetAsync("payments:item:1", It.IsAny<PaymentOutputDto>(), It.IsAny<TimeSpan>()), Times.Once);
    }
}
