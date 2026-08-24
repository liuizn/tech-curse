using FluentAssertions;
using Moq;
using TechCurse.Application.DTOs;
using TechCurse.Application.Features.Payments.Queries.GetPayments;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Entities;
using TechCurse.Domain.Enums;
using Xunit;

namespace TechCurse.Application.UnitTests.Handlers.Payments;

public class GetPaymentsQueryHandlerTests
{
    private readonly Mock<IPaymentRepository> _paymentRepositoryMock = new();
    private readonly Mock<ICacheService> _cacheServiceMock = new();
    private readonly GetPaymentsQueryHandler _handler;

    public GetPaymentsQueryHandlerTests()
    {
        _handler = new GetPaymentsQueryHandler(_paymentRepositoryMock.Object, _cacheServiceMock.Object);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenCacheHit_ShouldReturnCachedResult()
    {
        // Arrange
        var searchParams = new PaginationParamsDto { PageNumber = 1, PageSize = 10, SortBy = "Id", SortDirection = "ASC" };
        var query = new GetPaymentsQuery(searchParams);

        var cachedResult = new PagedResultDto<PaymentOutputDto>(
            new List<PaymentOutputDto> { new(1, 10, 20, 100m, PaymentStatus.Paid, true, DateTime.UtcNow, null, null) },
            1, 1, 10
        );

        _cacheServiceMock.Setup(c => c.GetAsync<PagedResultDto<PaymentOutputDto>>(It.IsAny<string>()))
            .ReturnsAsync(cachedResult);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().Be(cachedResult);
        _paymentRepositoryMock.Verify(r => r.GetPagedAsync(It.IsAny<PaginationParamsDto>()), Times.Never);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenCacheMiss_ShouldFetchFromRepositoryAndCache()
    {
        // Arrange
        var searchParams = new PaginationParamsDto { PageNumber = 1, PageSize = 10, SortBy = "Id", SortDirection = "ASC" };
        var query = new GetPaymentsQuery(searchParams);

        _cacheServiceMock.Setup(c => c.GetAsync<PagedResultDto<PaymentOutputDto>>(It.IsAny<string>()))
            .ReturnsAsync((PagedResultDto<PaymentOutputDto>?)null);

        var payments = new List<Payment>
        {
            new() { PaymentId = 1, EnrollmentId = 10, StudentId = 20, Amount = 100m, Status = PaymentStatus.Paid, IsActive = true, CreatedAt = DateTime.UtcNow }
        };

        _paymentRepositoryMock.Setup(r => r.GetPagedAsync(searchParams))
            .ReturnsAsync((payments, 1));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(1);
        result.Items.Should().HaveCount(1);

        _paymentRepositoryMock.Verify(r => r.GetPagedAsync(searchParams), Times.Once);
        _cacheServiceMock.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<PagedResultDto<PaymentOutputDto>>(), It.IsAny<TimeSpan>()), Times.Once);
    }
}
