using FluentAssertions;
using Moq;
using TechCurse.src.Application.DTOs;
using TechCurse.src.Application.Features.Courses.Queries.GetCourses;
using TechCurse.src.Application.Interfaces;
using TechCurse.src.Domain.Entities;
using Xunit;

namespace TechCurse.Test.Unit.Handlers.Courses;

public class GetCoursesQueryHandlerTests
{
    private readonly Mock<ICourseRepository> _courseRepositoryMock = new();
    private readonly Mock<ICacheService> _cacheServiceMock = new();
    private readonly GetCoursesQueryHandler _handler;

    public GetCoursesQueryHandlerTests()
    {
        _handler = new GetCoursesQueryHandler(_courseRepositoryMock.Object, _cacheServiceMock.Object);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenCacheHit_ShouldReturnCachedResult()
    {
        // Arrange
        var searchParams = new CoursePaginationParamsDto { PageNumber = 1, PageSize = 10, SortBy = "Id", SortDirection = "ASC" };
        var query = new GetCoursesQuery(searchParams);

        var cachedResult = new PagedResultDto<CourseOutputDto>(
            new List<CourseOutputDto> { new(1, "Curso Cache", "Desc", "Tech", 40, DateTime.UtcNow) },
            1, 1, 10
        );

        _cacheServiceMock.Setup(c => c.GetAsync<PagedResultDto<CourseOutputDto>>(It.IsAny<string>()))
            .ReturnsAsync(cachedResult);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().Be(cachedResult);
        _courseRepositoryMock.Verify(r => r.GetPagedAsync(It.IsAny<CoursePaginationParamsDto>()), Times.Never);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenCacheMiss_ShouldFetchFromRepoAndSetCache()
    {
        // Arrange
        var searchParams = new CoursePaginationParamsDto { PageNumber = 1, PageSize = 10, SortBy = "Id", SortDirection = "ASC", Categoria = "Tech" };
        var query = new GetCoursesQuery(searchParams);

        _cacheServiceMock.Setup(c => c.GetAsync<PagedResultDto<CourseOutputDto>>(It.IsAny<string>()))
            .ReturnsAsync((PagedResultDto<CourseOutputDto>?)null);

        var courses = new List<Course>
        {
            new() { CourseId = 1, Titulo = "Curso DB", Descricao = "Desc", Categoria = "Tech", CargaHoraria = 30, DataCriacao = DateTime.UtcNow }
        };

        _courseRepositoryMock.Setup(r => r.GetPagedAsync(searchParams))
            .ReturnsAsync((courses, 1));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(1);
        result.Items.Should().HaveCount(1);
        result.Items.First().Titulo.Should().Be("Curso DB");

        _courseRepositoryMock.Verify(r => r.GetPagedAsync(searchParams), Times.Once);
        _cacheServiceMock.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<PagedResultDto<CourseOutputDto>>(), It.IsAny<TimeSpan>()), Times.Once);
    }
}
