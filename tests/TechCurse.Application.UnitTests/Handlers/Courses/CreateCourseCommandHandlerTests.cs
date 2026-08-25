using FluentAssertions;
using Moq;
using TechCurse.Application.DTOs;
using TechCurse.Application.Features.Courses.Commands.CreateCourse;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Entities;
using Xunit;

namespace TechCurse.Application.UnitTests.Handlers.Courses;

public class CreateCourseCommandHandlerTests
{
    private readonly Mock<ICourseRepository> _courseRepositoryMock = new();
    private readonly Mock<ICacheService> _cacheServiceMock = new();
    private readonly CreateCourseCommandHandler _handler;

    public CreateCourseCommandHandlerTests()
    {
        _handler = new CreateCourseCommandHandler(_courseRepositoryMock.Object, _cacheServiceMock.Object);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenValidCommand_ShouldCreateCourseAndClearListCache()
    {
        var command = new CreateCourseCommand("Curso C#", "Curso completo de C#", "Tecnologia", 40);

        _courseRepositoryMock.Setup(r => r.AddAsync(It.IsAny<Course>()))
            .Callback<Course>(c => c.CourseId = 1)
            .Returns(Task.CompletedTask);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Titulo.Should().Be("Curso C#");
        result.Descricao.Should().Be("Curso completo de C#");
        result.Categoria.Should().Be("Tecnologia");
        result.CargaHoraria.Should().Be(40);

        _courseRepositoryMock.Verify(r => r.AddAsync(It.Is<Course>(c => c.Titulo == "Curso C#")), Times.Once);
        _cacheServiceMock.Verify(c => c.SetAsync(It.Is<string>(k => k.Contains("courses:item:")), It.IsAny<CourseOutputDto>(), It.IsAny<TimeSpan>()), Times.Once);
        _cacheServiceMock.Verify(c => c.RemoveByPrefixAsync("courses:list:"), Times.Once);
    }
}
