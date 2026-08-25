using FluentAssertions;
using Moq;
using TechCurse.Application.DTOs;
using TechCurse.Application.Features.Payments.Queries.GetPaymentsByStudentId;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Entities;
using TechCurse.Domain.Enums;
using TechCurse.Domain.Exceptions;
using Xunit;

namespace TechCurse.Application.UnitTests.Handlers.Payments;

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
        _studentRepositoryMock.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync((Student?)null);

        var query = new GetPaymentsByStudentIdQuery(1, new PaginationParamsDto());

        var act = () => _handler.Handle(query, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Estudante não encontrado.");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenNonAdminAndMismatchUser_ShouldThrowNotAllowedException()
    {
        var student = new Student { StudentId = 1, IdentityUserId = "user-real" };
        _studentRepositoryMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(student);

        _currentUserServiceMock.Setup(u => u.GetUserId()).Returns("user-fake");
        _currentUserServiceMock.Setup(u => u.IsInRole(UserRole.Admin)).Returns(false);

        var query = new GetPaymentsByStudentIdQuery(1, new PaginationParamsDto());

        var act = () => _handler.Handle(query, CancellationToken.None);

        await act.Should().ThrowAsync<NotAllowedException>()
            .WithMessage("Você não possuí permissão suficiente para acessar este registro!");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenCacheHit_ShouldReturnCachedPayments()
    {
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

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Should().Be(cachedResult);
    }
}
