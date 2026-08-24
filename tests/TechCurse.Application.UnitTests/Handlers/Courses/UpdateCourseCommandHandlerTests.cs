using FluentAssertions;
using MediatR;
using Moq;
using TechCurse.Application.DTOs;
using TechCurse.Application.Features.Courses.Commands.UpdateCourse;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Entities;
using TechCurse.Domain.Exceptions;
using Xunit;

namespace TechCurse.Application.UnitTests.Handlers.Courses;

public class UpdateCourseCommandHandlerTests
{
    private readonly Mock<ICourseRepository> _courseRepositoryMock = new();
    private readonly Mock<ICacheService> _cacheServiceMock = new();
    private readonly UpdateCourseCommandHandler _handler;

    public UpdateCourseCommandHandlerTests()
    {
        _handler = new UpdateCourseCommandHandler(_courseRepositoryMock.Object, _cacheServiceMock.Object);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenCourseExists_ShouldUpdateCourseAndInvalidateCache()
    {
        // Arrange
        var existingCourse = new Course
        {
            CourseId = 1,
            Titulo = "Título Antigo",
            Descricao = "Descrição Antiga",
            Categoria = "Tecnologia",
            CargaHoraria = 20,
            DataCriacao = DateTime.UtcNow.AddDays(-10)
        };

        _courseRepositoryMock.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(existingCourse);

        var command = new UpdateCourseCommand(1, "Título Novo", "Descrição Nova", "Tecnologia", 30);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(MediatR.Unit.Value);
        existingCourse.Titulo.Should().Be("Título Novo");
        existingCourse.Descricao.Should().Be("Descrição Nova");
        existingCourse.CargaHoraria.Should().Be(30);

        _courseRepositoryMock.Verify(r => r.UpdateAsync(existingCourse), Times.Once);
        _cacheServiceMock.Verify(c => c.SetAsync("courses:item:1", It.IsAny<CourseOutputDto>(), It.IsAny<TimeSpan>()), Times.Once);
        _cacheServiceMock.Verify(c => c.RemoveByPrefixAsync("courses:list:"), Times.Once);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenCourseDoesNotExist_ShouldThrowNotFoundException()
    {
        // Arrange
        _courseRepositoryMock.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync((Course?)null);

        var command = new UpdateCourseCommand(1, "Título Novo", "Descrição Nova", "Tecnologia", 30);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Curso não encontrado.");

        _courseRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Course>()), Times.Never);
    }
}
