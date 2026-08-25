using FluentAssertions;
using Moq;
using TechCurse.Application.DTOs;
using TechCurse.Application.Features.Courses.Queries.GetCourseById;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Entities;
using TechCurse.Domain.Exceptions;
using Xunit;

namespace TechCurse.Application.UnitTests.Handlers.Courses;

public class GetCourseByIdQueryHandlerTests
{
    private readonly Mock<ICourseRepository> _courseRepositoryMock = new();
    private readonly Mock<ICacheService> _cacheServiceMock = new();
    private readonly GetCourseByIdQueryHandler _handler;

    public GetCourseByIdQueryHandlerTests()
    {
        _handler = new GetCourseByIdQueryHandler(_courseRepositoryMock.Object, _cacheServiceMock.Object);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenCourseIsInCache_ShouldReturnCachedResult()
    {
        var cachedDto = new CourseOutputDto(1, "Curso Cache", "Descrição Cache", "Tecnologia", 40, DateTime.UtcNow);
        _cacheServiceMock.Setup(c => c.GetAsync<CourseOutputDto>("courses:item:1"))
            .ReturnsAsync(cachedDto);

        var query = new GetCourseByIdQuery(1);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Should().Be(cachedDto);
        _courseRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenCourseNotInCache_ShouldGetFromRepositoryAndCacheIt()
    {
        _cacheServiceMock.Setup(c => c.GetAsync<CourseOutputDto>("courses:item:1"))
            .ReturnsAsync((CourseOutputDto?)null);

        var course = new Course
        {
            CourseId = 1,
            Titulo = "Curso DB",
            Descricao = "Descrição DB",
            Categoria = "Tecnologia",
            CargaHoraria = 50,
            DataCriacao = DateTime.UtcNow
        };

        _courseRepositoryMock.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(course);

        var query = new GetCourseByIdQuery(1);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result.Id.Should().Be(1);
        result.Titulo.Should().Be("Curso DB");

        _courseRepositoryMock.Verify(r => r.GetByIdAsync(1), Times.Once);
        _cacheServiceMock.Verify(c => c.SetAsync("courses:item:1", It.IsAny<CourseOutputDto>(), It.IsAny<TimeSpan>()), Times.Once);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenCourseNotFoundInRepository_ShouldThrowNotFoundException()
    {
        _cacheServiceMock.Setup(c => c.GetAsync<CourseOutputDto>("courses:item:1"))
            .ReturnsAsync((CourseOutputDto?)null);

        _courseRepositoryMock.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync((Course?)null);

        var query = new GetCourseByIdQuery(1);

        var act = () => _handler.Handle(query, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Curso não encontrado.");
    }
}
