using FluentAssertions;
using Moq;
using TechCurse.src.Application.DTOs;
using TechCurse.src.Application.Features.Students.Queries.GetStudents;
using TechCurse.src.Application.Interfaces;
using TechCurse.src.Domain.Entities;
using Xunit;

namespace TechCurse.Test.Unit.Handlers.Students;

public class GetStudentsQueryHandlerTests
{
    private readonly Mock<IStudentRepository> _studentRepositoryMock = new();
    private readonly Mock<ICacheService> _cacheServiceMock = new();
    private readonly GetStudentsQueryHandler _handler;

    public GetStudentsQueryHandlerTests()
    {
        _handler = new GetStudentsQueryHandler(_studentRepositoryMock.Object, _cacheServiceMock.Object);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenCacheHit_ShouldReturnCachedResult()
    {
        // Arrange
        var searchParams = new PaginationParamsDto { PageNumber = 1, PageSize = 10, SortBy = "Id", SortDirection = "ASC" };
        var query = new GetStudentsQuery(searchParams);

        var cachedResult = new PagedResultDto<StudentOutputDto>(
            new List<StudentOutputDto> { new(1, "Student Cache", "student@example.com", DateTime.UtcNow) },
            1, 1, 10
        );

        _cacheServiceMock.Setup(c => c.GetAsync<PagedResultDto<StudentOutputDto>>(It.IsAny<string>()))
            .ReturnsAsync(cachedResult);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().Be(cachedResult);
        _studentRepositoryMock.Verify(r => r.GetPagedAsync(It.IsAny<PaginationParamsDto>()), Times.Never);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenCacheMiss_ShouldFetchFromRepositoryAndCache()
    {
        // Arrange
        var searchParams = new PaginationParamsDto { PageNumber = 1, PageSize = 10, SortBy = "Id", SortDirection = "ASC" };
        var query = new GetStudentsQuery(searchParams);

        _cacheServiceMock.Setup(c => c.GetAsync<PagedResultDto<StudentOutputDto>>(It.IsAny<string>()))
            .ReturnsAsync((PagedResultDto<StudentOutputDto>?)null);

        var students = new List<Student>
        {
            new() { StudentId = 1, Nome = "Student DB", Email = "studentdb@example.com", DataCadastro = DateTime.UtcNow }
        };

        _studentRepositoryMock.Setup(r => r.GetPagedAsync(searchParams))
            .ReturnsAsync((students, 1));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(1);
        result.Items.Should().HaveCount(1);

        _studentRepositoryMock.Verify(r => r.GetPagedAsync(searchParams), Times.Once);
        _cacheServiceMock.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<PagedResultDto<StudentOutputDto>>(), It.IsAny<TimeSpan>()), Times.Once);
    }
}
