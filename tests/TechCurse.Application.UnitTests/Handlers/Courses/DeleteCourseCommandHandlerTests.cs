using FluentAssertions;
using MediatR;
using Moq;
using TechCurse.Application.Features.Courses.Commands.DeleteCourse;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Entities;
using TechCurse.Domain.Exceptions;
using Xunit;

namespace TechCurse.Application.UnitTests.Handlers.Courses;

public class DeleteCourseCommandHandlerTests
{
    private readonly Mock<ICourseRepository> _courseRepositoryMock = new();
    private readonly Mock<ICacheService> _cacheServiceMock = new();
    private readonly DeleteCourseCommandHandler _handler;

    public DeleteCourseCommandHandlerTests()
    {
        _handler = new DeleteCourseCommandHandler(_courseRepositoryMock.Object, _cacheServiceMock.Object);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenCourseExistsAndHasNoEnrollments_ShouldDeleteCourseAndInvalidateCache()
    {
        // Arrange
        var course = new Course
        {
            CourseId = 1,
            Titulo = "Curso C#",
            Descricao = "Descrição",
            Categoria = "Tecnologia",
            CargaHoraria = 40
        };

        _courseRepositoryMock.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(course);

        _courseRepositoryMock.Setup(r => r.HasEnrollmentsAsync(1))
            .ReturnsAsync(false);

        var command = new DeleteCourseCommand(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(MediatR.Unit.Value);
        _courseRepositoryMock.Verify(r => r.DeleteAsync(course), Times.Once);
        _cacheServiceMock.Verify(c => c.RemoveAsync("courses:item:1"), Times.Once);
        _cacheServiceMock.Verify(c => c.RemoveByPrefixAsync("courses:list:"), Times.Once);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenCourseDoesNotExist_ShouldThrowNotFoundException()
    {
        // Arrange
        _courseRepositoryMock.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync((Course?)null);

        var command = new DeleteCourseCommand(1);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Curso não encontrado.");

        _courseRepositoryMock.Verify(r => r.DeleteAsync(It.IsAny<Course>()), Times.Never);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenCourseHasEnrollments_ShouldThrowConflictException()
    {
        // Arrange
        var course = new Course
        {
            CourseId = 1,
            Titulo = "Curso C#",
            Descricao = "Descrição",
            Categoria = "Tecnologia",
            CargaHoraria = 40
        };

        _courseRepositoryMock.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(course);

        _courseRepositoryMock.Setup(r => r.HasEnrollmentsAsync(1))
            .ReturnsAsync(true);

        var command = new DeleteCourseCommand(1);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("O curso possui matrículas ativas.");

        _courseRepositoryMock.Verify(r => r.DeleteAsync(It.IsAny<Course>()), Times.Never);
    }
}
