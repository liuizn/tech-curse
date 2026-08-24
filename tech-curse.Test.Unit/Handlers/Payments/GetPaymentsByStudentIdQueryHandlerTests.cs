using FluentAssertions;
using Moq;
using TechCurse.src.Application.DTOs;
using TechCurse.src.Application.Features.Payments.Queries.GetPaymentsByStudentId;
using TechCurse.src.Application.Interfaces;
using TechCurse.src.Domain.Entities;
using TechCurse.src.Domain.Enums;
using TechCurse.src.Domain.Exceptions;
using Xunit;

namespace TechCurse.Test.Unit.Handlers.Payments;

public class GetPaymentsByStudentIdQueryHandlerTests
{
    private readonly Mock<IPaymentRepository> _paymentRepositoryMock = new();
    private readonly Mock<IStudentRepository> _studentRepositoryMock = new();
    private readonly Mock<ICacheService> _cacheServiceMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly GetPaymentsByStudentIdQueryHandler _handler;

    public GetPaymentsByStudentIdQueryHandlerTests()
    {
        _handler = new GetPaymentsByStudentIdQueryHandler(
            _paymentRepositoryMock.Object,
            _studentRepositoryMock.Object,
            _cacheServiceMock.Object,
            _currentUserServiceMock.Object
        );
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenStudentNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        _studentRepositoryMock.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync((Student?)null);

        var query = new GetPaymentsByStudentIdQuery(1, new PaginationParamsDto());

        // Act
        var act = () => _handler.Handle(query, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Estudante não encontrado.");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenNonAdminAndMismatchUser_ShouldThrowNotAllowedException()
    {
        // Arrange
        var student = new Student { StudentId = 1, IdentityUserId = "user-real" };
        _studentRepositoryMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(student);

        _currentUserServiceMock.Setup(u => u.GetUserId()).Returns("user-fake");
        _currentUserServiceMock.Setup(u => u.IsInRole(UserRole.Admin)).Returns(false);

        var query = new GetPaymentsByStudentIdQuery(1, new PaginationParamsDto());

        // Act
        var act = () => _handler.Handle(query, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotAllowedException>()
            .WithMessage("Você não possuí permissão suficiente para acessar este registro!");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenCacheHit_ShouldReturnCachedPayments()
    {
        // Arrange
        var student = new Student { StudentId = 1, IdentityUserId = "user-real" };
        _studentRepositoryMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(student);

        _currentUserServiceMock.Setup(u => u.GetUserId()).Returns("user-real");
        _currentUserServiceMock.Setup(u => u.IsInRole(UserRole.Admin)).Returns(false);

        var cachedResult = new PagedResultDto<PaymentOutputDto>(
            new List<PaymentOutputDto> { new(1, 1, 1, 100m, PaymentStatus.Paid, true, DateTime.UtcNow, null, null) },
            1, 1, 10
        );

        _cacheServiceMock.Setup(c => c.GetAsync<PagedResultDto<PaymentOutputDto>>(It.IsAny<string>()))
            .ReturnsAsync(cachedResult);

        var query = new GetPaymentsByStudentIdQuery(1, new PaginationParamsDto { PageNumber = 1, PageSize = 10 });

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().Be(cachedResult);
    }
}
